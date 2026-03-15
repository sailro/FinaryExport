using ClosedXML.Excel;

namespace FinaryExport.Export.Formatting;

public static class ExcelStyles
{
	public const string DefaultCurrencyFormat = "#,##0.00";
	public const string PercentFormat = "0.00%";
	public const string DateFormat = "yyyy-MM-dd";

	// Generates an Excel number format with the currency symbol prefix.
	// E.g., "$" -> "\"$ \"#,##0.00", "€" -> "\"€ \"#,##0.00"
	public static string GetCurrencyFormat(string? symbol)
	{
		if (string.IsNullOrEmpty(symbol))
			return DefaultCurrencyFormat;

		// Excel format: symbol in quotes, followed by space, then number format
		return $"\"{symbol} \"{DefaultCurrencyFormat}";
	}

	public static void ApplyHeaderStyle(IXLRow headerRow)
	{
		headerRow.Style.Font.Bold = true;
		headerRow.Style.Fill.BackgroundColor = XLColor.FromArgb(0x4472C4);
		headerRow.Style.Font.FontColor = XLColor.White;
		headerRow.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
	}

	public static void FinalizeSheet(IXLWorksheet ws, int headerRow = 1)
	{
		ws.Columns().AdjustToContents();
		ws.SheetView.FreezeRows(headerRow);
	}
}
