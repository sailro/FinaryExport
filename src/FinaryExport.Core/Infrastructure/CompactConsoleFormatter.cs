using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace FinaryExport.Infrastructure;

// Compact single-line console log formatter.
// Output: "dbug: TokenRefreshService: Token refresh service stopping..."
public sealed class CompactConsoleFormatter() : ConsoleFormatter(FormatterName)
{
	public const string FormatterName = "compact";

	public override void Write<TState>(
		in LogEntry<TState> logEntry,
		IExternalScopeProvider? scopeProvider,
		TextWriter textWriter)
	{
		var message = logEntry.Formatter.Invoke(logEntry.State, logEntry.Exception);
		if (message is null)
			return;

		var level = logEntry.LogLevel switch
		{
			LogLevel.Trace => "trce",
			LogLevel.Debug => "dbug",
			LogLevel.Information => "info",
			LogLevel.Warning => "warn",
			LogLevel.Error => "fail",
			LogLevel.Critical => "crit",
			_ => "????"
		};

		// Extract short class name from full namespace (e.g. "FinaryExport.Auth.ClerkAuthClient" → "ClerkAuthClient")
		var category = logEntry.Category;
		var lastDot = category.LastIndexOf('.');
		if (lastDot >= 0)
			category = category[(lastDot + 1)..];

		textWriter.Write(level);
		textWriter.Write(": ");
		textWriter.Write(category);
		textWriter.Write(": ");
		textWriter.WriteLine(message);

		if (logEntry.Exception is not null)
			textWriter.WriteLine(logEntry.Exception.ToString());
	}
}
