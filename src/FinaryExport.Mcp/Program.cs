using FinaryExport.Api;
using FinaryExport.Auth;
using FinaryExport.Configuration;
using FinaryExport.Infrastructure;
using FinaryExport.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

var builder = Host.CreateApplicationBuilder(args);

// Redirect all console logging to stderr — stdout is reserved for MCP stdio transport
builder.Services.Configure<ConsoleLoggerOptions>(options =>
	options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Logging.AddFilter("System.Net.Http", LogLevel.Warning);

builder.Services.Configure<FinaryOptions>(builder.Configuration.GetSection(FinaryOptions.SectionName));

// Core services (auth, API client, HTTP, rate limiter, session store)
builder.Services.AddFinaryCore();

// MCP tools snapshot the selected profile into a fresh Core client per invocation.
// The CLI exporter and MCP host share Core types, not mutable client instances.
builder.Services.AddSingleton<IMcpFinaryApiClientFactory, McpFinaryApiClientFactory>();

// If no session.dat exists, MCP Elicitation prompts the user for credentials
builder.Services.AddSingleton<ICredentialPrompt, McpCredentialPrompt>();

builder.Services
	.AddMcpServer(options => options.ServerInstructions = """
		This server never mutates Finary data. Portfolio totals returned by get_portfolio_summary are authoritative for the active profile; never recompute totals by summing account or holding rows. Account IDs may occur in more than one category because categories can contain distinct value components; get_all_accounts groups those components under one account without discarding them. Every response declares its scope; portfolio data uses an isolated snapshot of the active profile, while identity and profile-list tools use their own global scopes. Use get_profiles and set_active_profile before comparing family members. Monetary values are decimal strings in the declared display currency. Transaction periods are trailing windows ending at the current time.
		""")
	.WithStdioServerTransport()
	.WithToolsFromAssembly();

await builder.Build().RunAsync();

public partial class Program;
