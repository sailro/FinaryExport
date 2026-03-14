using System.CommandLine;
using System.Diagnostics;
using FinaryExport.Api;
using FinaryExport.Auth;
using FinaryExport.Configuration;
using FinaryExport.Export;
using FinaryExport.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

var rootCommand = new RootCommand("FinaryExport — Export Finary wealth data to xlsx");

var outputOption = new Option<string?>("--output", "Output xlsx file path");
var periodOption = new Option<string?>("--period", "Time period (1d, 1w, 1m, 3m, 6m, 1y, all)");
var clearSessionOption = new Option<bool>("--clear-session", "Force re-authentication (discard saved session)");

// Export command (default)
var exportCommand = new Command("export", "Export Finary data to xlsx");
exportCommand.Options.Add(outputOption);
exportCommand.Options.Add(periodOption);
exportCommand.Options.Add(clearSessionOption);

exportCommand.SetAction(async result =>
{
    await RunExportAsync(result.GetValue(outputOption), result.GetValue(periodOption), result.GetValue(clearSessionOption));
});

// Clear session command
var clearCommand = new Command("clear-session", "Clear saved authentication session");
clearCommand.SetAction(async _ =>
{
    var builder = Host.CreateApplicationBuilder([]);
    ConfigureHost(builder, null, null, true);
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
rootCommand.Options.Add(periodOption);
rootCommand.Options.Add(clearSessionOption);
rootCommand.SetAction(async result =>
{
    await RunExportAsync(result.GetValue(outputOption), result.GetValue(periodOption), result.GetValue(clearSessionOption));
});

return await rootCommand.Parse(args).InvokeAsync();

// ── Implementation ──

static async Task RunExportAsync(string? output, string? period, bool clearSession)
{
    var sw = Stopwatch.StartNew();
    Console.WriteLine("FinaryExport v1.0.0");

    var builder = Host.CreateApplicationBuilder([]);
    ConfigureHost(builder, output, period, clearSession);
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
        var profileContext = new ExportContext { UseDisplayValues = true };
        for (var i = 0; i < profiles.Count; i++)
        {
            var profile = profiles[i];
            Console.WriteLine($"Exporting profile {i + 1}/{profiles.Count}: {profile.ProfileName}...");

            apiClient.SetOrganizationContext(profile.OrgId, profile.MembershipId);

            var outputPath = BuildOutputPath(options.OutputPath, profile.ProfileName);
            await exporter.ExportAsync(outputPath, apiClient, profileContext);
            Console.WriteLine($"  → {outputPath}");
        }

        // 4. Export unified file (aggregated across ALL memberships)
        if (profiles.Count > 0)
        {
            Console.WriteLine($"Exporting unified ({profiles.Count} profiles)...");
            var unifiedApi = new UnifiedFinaryApiClient(apiClient, profiles, logger);
            var unifiedContext = new ExportContext { UseDisplayValues = false };
            var unifiedPath = BuildUnifiedPath(options.OutputPath);
            await exporter.ExportAsync(unifiedPath, unifiedApi, unifiedContext);
            Console.WriteLine($"  → {unifiedPath}");
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

static void ConfigureHost(HostApplicationBuilder builder, string? output, string? period, bool clearSession)
{
    builder.Configuration
        .AddJsonFile("appsettings.json", optional: true)
        .AddEnvironmentVariables()
        .AddUserSecrets<Program>(optional: true);

    // Use compact single-line log format
    builder.Logging.AddConsoleFormatter<
        CompactConsoleFormatter,
        ConsoleFormatterOptions>();
    builder.Logging.AddConsole(options =>
        options.FormatterName = CompactConsoleFormatter.FormatterName);

    builder.Services.Configure<FinaryOptions>(builder.Configuration.GetSection(FinaryOptions.SectionName));

    // Apply CLI overrides via post-configure
    builder.Services.PostConfigure<FinaryOptions>(config =>
    {
        if (output is not null) config.OutputPath = output;
        if (period is not null) config.Period = period;
        if (clearSession) config.ClearSession = true;
    });

    builder.Services.AddFinaryExport();
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

// Marker type for user secrets
public partial class Program;
