using ClosedXML.Excel;

namespace FinaryExport.Export.Formatting;

public static class ExcelStyles
{
	public const string CurrencyFormat = "#,##0.00";
	public const string PercentFormat = "0.00%";
	public const string DateFormat = "yyyy-MM-dd";

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
