using ClosedXML.Excel;
using FinaryExport.Export.Formatting;
using FluentAssertions;

namespace FinaryExport.Tests.Export;

public sealed class ExcelStylesTests
{
	// ================================================================
	// GetCurrencyFormat
	// ================================================================

	[Fact]
	public void GetCurrencyFormat_NullSymbol_ReturnsDefaultFormat()
	{
		ExcelStyles.GetCurrencyFormat(null).Should().Be("#,##0.00");
	}

	[Fact]
	public void GetCurrencyFormat_EmptySymbol_ReturnsDefaultFormat()
	{
		ExcelStyles.GetCurrencyFormat("").Should().Be("#,##0.00");
	}

	[Fact]
	public void GetCurrencyFormat_EuroSymbol_ReturnsFormattedString()
	{
		ExcelStyles.GetCurrencyFormat("€").Should().Be("\"€ \"#,##0.00");
	}

	[Fact]
	public void GetCurrencyFormat_DollarSymbol_ReturnsFormattedString()
	{
		ExcelStyles.GetCurrencyFormat("$").Should().Be("\"$ \"#,##0.00");
	}

	[Fact]
	public void GetCurrencyFormat_PoundSymbol_ReturnsFormattedString()
	{
		ExcelStyles.GetCurrencyFormat("£").Should().Be("\"£ \"#,##0.00");
	}

	// ================================================================
	// Constants
	// ================================================================

	[Fact]
	public void DefaultCurrencyFormat_IsCorrect()
	{
		ExcelStyles.DefaultCurrencyFormat.Should().Be("#,##0.00");
	}

	[Fact]
	public void PercentFormat_IsCorrect()
	{
		ExcelStyles.PercentFormat.Should().Be("0.00%");
	}

	[Fact]
	public void DateFormat_IsCorrect()
	{
		ExcelStyles.DateFormat.Should().Be("yyyy-MM-dd");
	}

	// ================================================================
	// ApplyHeaderStyle
	// ================================================================

	[Fact]
	public void ApplyHeaderStyle_SetsBold()
	{
		using var wb = new XLWorkbook();
		var ws = wb.AddWorksheet("Test");
		ws.Cell("A1").Value = "Header";

		ExcelStyles.ApplyHeaderStyle(ws.Row(1));

		ws.Row(1).Style.Font.Bold.Should().BeTrue();
	}

	[Fact]
	public void ApplyHeaderStyle_SetsBackgroundColor()
	{
		using var wb = new XLWorkbook();
		var ws = wb.AddWorksheet("Test");
		ws.Cell("A1").Value = "Header";

		ExcelStyles.ApplyHeaderStyle(ws.Row(1));

		// Verify it's the expected blue color (0x4472C4)
		var color = ws.Row(1).Style.Fill.BackgroundColor;
		color.Should().Be(XLColor.FromArgb(0x4472C4));
	}

	[Fact]
	public void ApplyHeaderStyle_SetsWhiteFont()
	{
		using var wb = new XLWorkbook();
		var ws = wb.AddWorksheet("Test");
		ws.Cell("A1").Value = "Header";

		ExcelStyles.ApplyHeaderStyle(ws.Row(1));

		ws.Row(1).Style.Font.FontColor.Should().Be(XLColor.White);
	}

	[Fact]
	public void ApplyHeaderStyle_CentersText()
	{
		using var wb = new XLWorkbook();
		var ws = wb.AddWorksheet("Test");
		ws.Cell("A1").Value = "Header";

		ExcelStyles.ApplyHeaderStyle(ws.Row(1));

		ws.Row(1).Style.Alignment.Horizontal.Should().Be(XLAlignmentHorizontalValues.Center);
	}

	// ================================================================
	// FinalizeSheet
	// ================================================================

	[Fact]
	public void FinalizeSheet_DoesNotThrow()
	{
		using var wb = new XLWorkbook();
		var ws = wb.AddWorksheet("Test");
		ws.Cell("A1").Value = "Name";
		ws.Cell("A2").Value = "Data";

		var act = () => ExcelStyles.FinalizeSheet(ws);
		act.Should().NotThrow();
	}

	[Fact]
	public void FinalizeSheet_CustomHeaderRow_DoesNotThrow()
	{
		using var wb = new XLWorkbook();
		var ws = wb.AddWorksheet("Test");
		ws.Cell("A1").Value = "Title";
		ws.Cell("A3").Value = "Header";
		ws.Cell("A4").Value = "Data";

		var act = () => ExcelStyles.FinalizeSheet(ws, headerRow: 3);
		act.Should().NotThrow();
	}
}
