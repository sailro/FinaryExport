using FinaryExport.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinaryExport.Tests.Infrastructure;

public sealed class CompactConsoleFormatterTests
{
	[Fact]
	public void FormatterName_IsCompact()
	{
		CompactConsoleFormatter.FormatterName.Should().Be("compact");
	}

	[Theory]
	[InlineData(LogLevel.Trace, "trce")]
	[InlineData(LogLevel.Debug, "dbug")]
	[InlineData(LogLevel.Information, "info")]
	[InlineData(LogLevel.Warning, "warn")]
	[InlineData(LogLevel.Error, "fail")]
	[InlineData(LogLevel.Critical, "crit")]
	public void Write_LogLevel_MapsToCorrectAbbreviation(LogLevel level, string expected)
	{
		var formatter = new CompactConsoleFormatter();
		using var writer = new StringWriter();

		var entry = new LogEntry<string>(
			level, "FinaryExport.Auth.ClerkAuthClient", new EventId(0), "Test message",
			null, (s, _) => s);

		formatter.Write(entry, null, writer);

		writer.ToString().Should().StartWith(expected);
	}

	[Fact]
	public void Write_Category_ExtractsShortName()
	{
		var formatter = new CompactConsoleFormatter();
		using var writer = new StringWriter();

		var entry = new LogEntry<string>(
			LogLevel.Information, "FinaryExport.Auth.ClerkAuthClient", new EventId(0),
			"Token refreshed", null, (s, _) => s);

		formatter.Write(entry, null, writer);

		var output = writer.ToString();
		output.Should().Contain("ClerkAuthClient");
		output.Should().NotContain("FinaryExport.Auth.");
	}

	[Fact]
	public void Write_CategoryWithNoDots_UsedAsIs()
	{
		var formatter = new CompactConsoleFormatter();
		using var writer = new StringWriter();

		var entry = new LogEntry<string>(
			LogLevel.Information, "SimpleCategory", new EventId(0),
			"Message", null, (s, _) => s);

		formatter.Write(entry, null, writer);

		writer.ToString().Should().Contain("SimpleCategory");
	}

	[Fact]
	public void Write_IncludesMessage()
	{
		var formatter = new CompactConsoleFormatter();
		using var writer = new StringWriter();

		var entry = new LogEntry<string>(
			LogLevel.Warning, "Test.Category", new EventId(0),
			"Rate limit exceeded", null, (s, _) => s);

		formatter.Write(entry, null, writer);

		writer.ToString().Should().Contain("Rate limit exceeded");
	}

	[Fact]
	public void Write_WithException_PrintsExceptionDetails()
	{
		var formatter = new CompactConsoleFormatter();
		using var writer = new StringWriter();

		var exception = new InvalidOperationException("Something went wrong");
		var entry = new LogEntry<string>(
			LogLevel.Error, "Test.Handler", new EventId(0),
			"Request failed", exception, (s, _) => s);

		formatter.Write(entry, null, writer);

		var output = writer.ToString();
		output.Should().Contain("Request failed");
		output.Should().Contain("Something went wrong");
	}

	[Fact]
	public void Write_NullMessage_WritesNothing()
	{
		var formatter = new CompactConsoleFormatter();
		using var writer = new StringWriter();

		var entry = new LogEntry<string>(
			LogLevel.Information, "Test", new EventId(0),
			"state", null, (_, _) => null!);

		formatter.Write(entry, null, writer);

		writer.ToString().Should().BeEmpty();
	}

	[Fact]
	public void Write_OutputFormat_MatchesExpectedPattern()
	{
		var formatter = new CompactConsoleFormatter();
		using var writer = new StringWriter();

		var entry = new LogEntry<string>(
			LogLevel.Debug, "FinaryExport.Infrastructure.FinaryDelegatingHandler", new EventId(0),
			"Token refresh service stopping...", null, (s, _) => s);

		formatter.Write(entry, null, writer);

		// Expected: "dbug: FinaryDelegatingHandler: Token refresh service stopping...\n"
		writer.ToString().Trim().Should().Be("dbug: FinaryDelegatingHandler: Token refresh service stopping...");
	}
}
