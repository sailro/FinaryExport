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

// Replace raw IFinaryApiClient with auto-init decorator so MCP users
// don't have to manually call get_profiles + set_active_profile first
var rawDescriptor = builder.Services.First(d => d.ServiceType == typeof(IFinaryApiClient));
builder.Services.Remove(rawDescriptor);
builder.Services.AddSingleton<FinaryApiClient>();
builder.Services.AddSingleton<IFinaryApiClient, AutoInitFinaryApiClient>();

// If no session.dat exists, MCP Elicitation prompts the user for credentials
builder.Services.AddSingleton<ICredentialPrompt, McpCredentialPrompt>();

builder.Services
	.AddMcpServer()
	.WithStdioServerTransport()
	.WithToolsFromAssembly();

await builder.Build().RunAsync();

public partial class Program;
