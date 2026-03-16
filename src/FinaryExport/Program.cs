using System.CommandLine;
using System.Diagnostics;
using FinaryExport;
using FinaryExport.Api;
using FinaryExport.Auth;
using FinaryExport.Configuration;
using FinaryExport.Export;
using FinaryExport.Export.Sheets;
using FinaryExport.Infrastructure;
using FinaryExport.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

var rootCommand = new RootCommand("FinaryExport - Export Finary wealth data to xlsx");

var outputOption = new Option<string?>("--output") { Description = "Output xlsx file path" };

// Export command (default)
var exportCommand = new Command("export", "Export Finary data to xlsx");
exportCommand.Options.Add(outputOption);

exportCommand.SetAction(async result =>
{
	await RunExportAsync(result.GetValue(outputOption));
});

// Clear session command
var clearCommand = new Command("clear-session", "Clear saved authentication session");
clearCommand.SetAction(async _ =>
{
	var builder = Host.CreateApplicationBuilder([]);
	ConfigureHost(builder, null, true);
	using var host = builder.Build();

	var sessionStore = host.Services.GetRequiredService<ISessionStore>();
	await sessionStore.ClearSessionAsync();
	Console.WriteLine("Session cleared.");
});

// Version command
var versionCommand = new Command("version", "Show version information");
versionCommand.SetAction(_ =>
{
	Console.WriteLine("FinaryExport v1.0.0");
});

rootCommand.Subcommands.Add(exportCommand);
rootCommand.Subcommands.Add(clearCommand);
rootCommand.Subcommands.Add(versionCommand);

// Default behavior: run export when no subcommand is specified
rootCommand.Options.Add(outputOption);
rootCommand.SetAction(async result =>
{
	await RunExportAsync(result.GetValue(outputOption));
});

return await rootCommand.Parse(args).InvokeAsync();

// -- Implementation --

static async Task RunExportAsync(string? output)
{
	var sw = Stopwatch.StartNew();
	Console.WriteLine("FinaryExport v1.0.0");

	var builder = Host.CreateApplicationBuilder([]);
	ConfigureHost(builder, output);
	using var host = builder.Build();

	var options = host.Services.GetRequiredService<IOptions<FinaryOptions>>().Value;
	var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("FinaryExport");

	try
	{
		// Start hosted services (token refresh)
		await host.StartAsync();

		// 1. Authenticate
		Console.Write("Authenticating... ");
		var authClient = host.Services.GetRequiredService<ClerkAuthClient>();
		await authClient.LoginAsync(CancellationToken.None);
		Console.WriteLine($"OK (session: {authClient.SessionId[..Math.Min(12, authClient.SessionId.Length)]}...)");

		// 2. Discover all profiles
		var apiClient = host.Services.GetRequiredService<IFinaryApiClient>();
		var profiles = await apiClient.GetAllProfilesAsync();
		Console.WriteLine($"Found {profiles.Count} profile(s)");

		// 3. Export one xlsx per profile (ownership-adjusted values)
		var exporter = host.Services.GetRequiredService<IWorkbookExporter>();
		for (var i = 0; i < profiles.Count; i++)
		{
			var profile = profiles[i];
			Console.WriteLine($"Exporting profile {i + 1}/{profiles.Count}: {profile.ProfileName}...");

			apiClient.SetOrganizationContext(profile.OrgId, profile.MembershipId);

			// Detect display currency from first available account
			var displayCurrencySymbol = await DetectDisplayCurrencySymbolAsync(apiClient);
			var profileContext = new ExportContext
			{
				UseDisplayValues = true,
				DisplayCurrencySymbol = displayCurrencySymbol
			};

			var outputPath = BuildOutputPath(options.OutputPath, profile.ProfileName);
			await exporter.ExportAsync(outputPath, apiClient, profileContext);
			Console.WriteLine($"  -> {outputPath}");
		}

		// 4. Export unified file (aggregated across ALL memberships)
		if (profiles.Count > 0)
		{
			Console.WriteLine($"Exporting unified ({profiles.Count} profiles)...");
			var unifiedApi = new UnifiedFinaryApiClient(apiClient, profiles, logger);

			// For unified export, also try to detect currency symbol
			var unifiedCurrencySymbol = await DetectDisplayCurrencySymbolAsync(unifiedApi);
			var unifiedContext = new ExportContext
			{
				UseDisplayValues = false,
				DisplayCurrencySymbol = unifiedCurrencySymbol
			};
			var unifiedPath = BuildUnifiedPath(options.OutputPath);
			await exporter.ExportAsync(unifiedPath, unifiedApi, unifiedContext);
			Console.WriteLine($"  -> {unifiedPath}");
		}

		sw.Stop();
		Console.WriteLine($"Done in {sw.Elapsed.TotalSeconds:F1}s");
	}
	catch (System.Security.Authentication.AuthenticationException ex)
	{
		Console.Error.WriteLine($"Authentication failed: {ex.Message}");
		Environment.ExitCode = 1;
	}
	catch (Exception ex)
	{
		logger.LogError(ex, "Export failed");
		Console.Error.WriteLine($"Error: {ex.Message}");
		Environment.ExitCode = 2;
	}
	finally
	{
		await host.StopAsync();
	}
}

// Detects the display currency symbol by checking accounts across categories
static async Task<string?> DetectDisplayCurrencySymbolAsync(IFinaryApiClient api)
{
	// The display currency is the user's chosen currency in Finary settings (ui_configuration).
	// This is the currency all display values are converted to.
	try
	{
		var user = await api.GetCurrentUserAsync();
		var symbol = user?.UiConfiguration?.DisplayCurrency?.Symbol;
		if (!string.IsNullOrEmpty(symbol))
			return symbol;
	}
	catch
	{
		// Fall through to fallback
	}

	// Fallback: scan accounts for first currency symbol (native, not display — imperfect)
	foreach (var category in Enum.GetValues<AssetCategory>())
	{
		try
		{
			var accounts = await api.GetCategoryAccountsAsync(category);
			var symbol = accounts
				.Select(a => a.Currency?.Symbol)
				.FirstOrDefault(s => !string.IsNullOrEmpty(s));

			if (!string.IsNullOrEmpty(symbol))
				return symbol;
		}
		catch
		{
			// Continue to next category
		}
	}
	return null;
}

static void ConfigureHost(HostApplicationBuilder builder, string? output, bool clearSession = false)
{
	builder.Configuration
		.AddJsonFile("appsettings.json", optional: true)
		.AddEnvironmentVariables();

	// Use compact single-line log format
	builder.Logging.AddConsoleFormatter<
		CompactConsoleFormatter,
		ConsoleFormatterOptions>();
	builder.Logging.AddConsole(options =>
		options.FormatterName = CompactConsoleFormatter.FormatterName);

	// Suppress verbose HTTP client logging (request/response per-call)
	builder.Logging.AddFilter("System.Net.Http", LogLevel.Warning);

	builder.Services.Configure<FinaryOptions>(builder.Configuration.GetSection(FinaryOptions.SectionName));

	// Apply CLI overrides via post-configure
	builder.Services.PostConfigure<FinaryOptions>(config =>
	{
		if (output is not null) config.OutputPath = output;
	});

	// Core services (auth, API client, HTTP, rate limiter)
	builder.Services.AddFinaryCore();

	// CLI-specific: interactive credential prompt
	builder.Services.AddSingleton<ICredentialPrompt, ConsoleCredentialPrompt>();

	// Export services
	builder.Services.AddSingleton<IWorkbookExporter, WorkbookExporter>();
	builder.Services.AddSingleton<ISheetWriter, PortfolioSummarySheet>();
	builder.Services.AddSingleton<ISheetWriter, AccountsSheet>();
	builder.Services.AddSingleton<ISheetWriter, TransactionsSheet>();
	builder.Services.AddSingleton<ISheetWriter, DividendsSheet>();
	builder.Services.AddSingleton<ISheetWriter, HoldingsSheet>();
}

// Builds a per-profile output path from the user's --output option and the profile name.
// If the user specified a .xlsx path, the profile name is inserted before the extension.
// Otherwise the default pattern finary-export-{name}.xlsx is used in the same directory.
static string BuildOutputPath(string baseOutput, string profileName)
{
	var safeName = SanitizeFileName(profileName);
	var dir = Path.GetDirectoryName(baseOutput);
	var ext = Path.GetExtension(baseOutput);

	if (!string.Equals(ext, ".xlsx", StringComparison.OrdinalIgnoreCase))
		return Path.Combine(dir ?? ".", $"finary-export-{safeName}.xlsx");

	var stem = Path.GetFileNameWithoutExtension(baseOutput);
	return Path.Combine(dir ?? ".", $"{stem}-{safeName}.xlsx");
}

// Builds the output path for the unified (raw values) export file.
static string BuildUnifiedPath(string baseOutput)
{
	var dir = Path.GetDirectoryName(baseOutput);
	var ext = Path.GetExtension(baseOutput);

	if (!string.Equals(ext, ".xlsx", StringComparison.OrdinalIgnoreCase))
		return Path.Combine(dir ?? ".", "finary-export-unified.xlsx");

	var stem = Path.GetFileNameWithoutExtension(baseOutput);
	return Path.Combine(dir ?? ".", $"{stem}-unified.xlsx");
}

static string SanitizeFileName(string name)
{
	var invalid = Path.GetInvalidFileNameChars();
	var sanitized = new string([.. name.Select(c => invalid.Contains(c) ? '_' : c)]);
	return sanitized.Trim().ToLowerInvariant().Replace(' ', '-');
}

public partial class Program;
