using ClosedXML.Excel;
using FinaryExport.Api;
using FinaryExport.Export.Sheets;
using Microsoft.Extensions.Logging;

namespace FinaryExport.Export;

public sealed class WorkbookExporter(IEnumerable<ISheetWriter> writers, ILogger<WorkbookExporter> logger)
	: IWorkbookExporter
{
	public async Task ExportAsync(string outputPath, IFinaryApiClient api, ExportContext? context, CancellationToken ct)
	{
		var ctx = context ?? new ExportContext();
		using var workbook = new XLWorkbook();
		var errorCount = 0;

		foreach (var writer in writers)
		{
			try
			{
				logger.LogInformation("  Exporting {SheetName}...", writer.SheetName);
				await writer.WriteAsync(workbook, api, ctx, ct);
				logger.LogInformation("  \u2713 {SheetName}", writer.SheetName);
			}
			catch (OperationCanceledException) when (ct.IsCancellationRequested)
			{
				logger.LogWarning("  Export cancelled during {SheetName}", writer.SheetName);
				break;
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "  \u2717 {SheetName} failed: {Message}", writer.SheetName, ex.Message);
				AddErrorSheet(workbook, writer.SheetName, ex);
				errorCount++;
			}
		}

		// Ensure at least one sheet exists
		if (workbook.Worksheets.Count == 0)
		{
			var ws = workbook.Worksheets.Add("Info");
			ws.Cell("A1").Value = "No data was exported.";
		}

		workbook.SaveAs(outputPath);
		logger.LogInformation("Saved: {OutputPath} ({SheetCount} sheets, {Errors} errors)",
			outputPath, workbook.Worksheets.Count, errorCount);
	}

	private static void AddErrorSheet(XLWorkbook workbook, string sheetName, Exception ex)
	{
		var safeName = sheetName.Length > 28 ? sheetName[..28] : sheetName;
		var ws = workbook.Worksheets.Add($"{safeName} ERR");
		ws.Cell("A1").Value = $"Export failed for: {sheetName}";
		ws.Cell("A2").Value = $"Error: {ex.Message}";
		ws.Cell("A1").Style.Font.Bold = true;
		ws.Cell("A1").Style.Font.FontColor = XLColor.Red;
	}
}
