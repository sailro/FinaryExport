using ClosedXML.Excel;
using FinaryExport.Api;
using FinaryExport.Export.Sheets;
using Microsoft.Extensions.Logging;

namespace FinaryExport.Export;

public sealed class WorkbookExporter : IWorkbookExporter
{
    private readonly IEnumerable<ISheetWriter> _writers;
    private readonly ILogger<WorkbookExporter> _logger;

    public WorkbookExporter(IEnumerable<ISheetWriter> writers, ILogger<WorkbookExporter> logger)
    {
        _writers = writers;
        _logger = logger;
    }

    public async Task ExportAsync(string outputPath, IFinaryApiClient api, ExportContext? context, CancellationToken ct)
    {
        var ctx = context ?? new ExportContext();
        using var workbook = new XLWorkbook();
        int successCount = 0;
        int errorCount = 0;

        foreach (var writer in _writers)
        {
            try
            {
                _logger.LogInformation("  Exporting {SheetName}...", writer.SheetName);
                await writer.WriteAsync(workbook, api, ctx, ct);
                _logger.LogInformation("  \u2713 {SheetName}", writer.SheetName);
                successCount++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger.LogWarning("  Export cancelled during {SheetName}", writer.SheetName);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "  \u2717 {SheetName} failed: {Message}", writer.SheetName, ex.Message);
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
        _logger.LogInformation("Saved: {OutputPath} ({SheetCount} sheets, {Errors} errors)",
            outputPath, successCount + errorCount, errorCount);
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
