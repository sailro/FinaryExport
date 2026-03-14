using ClosedXML.Excel;
using FinaryExport.Api;
using FinaryExport.Export.Formatting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinaryExport.Export.Sheets;

public sealed class DividendsSheet(ILogger<DividendsSheet> logger) : ISheetWriter
{
    public string SheetName => "Dividends";

    public async Task WriteAsync(IXLWorkbook workbook, IFinaryApiClient api, ExportContext context, CancellationToken ct)
    {
        var dividends = await api.GetPortfolioDividendsAsync(ct);
        var ws = workbook.Worksheets.Add(SheetName);

        // Summary section
        ws.Cell("A1").Value = "Dividends Summary";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 14;

        ws.Cell("A3").Value = "Metric";
        ws.Cell("B3").Value = "Value";
        ExcelStyles.ApplyHeaderStyle(ws.Row(3));

        ws.Cell("A4").Value = "Annual Income";
        ws.Cell("B4").Value = dividends?.AnnualIncome ?? 0m;
        ws.Cell("B4").Style.NumberFormat.Format = ExcelStyles.CurrencyFormat;

        ws.Cell("A5").Value = "Past Income";
        ws.Cell("B5").Value = dividends?.PastIncome ?? 0m;
        ws.Cell("B5").Style.NumberFormat.Format = ExcelStyles.CurrencyFormat;

        ws.Cell("A6").Value = "Next Year";
        ws.Cell("B6").Value = dividends?.NextYear?.Sum(e => e.Value ?? 0m) ?? 0m;
        ws.Cell("B6").Style.NumberFormat.Format = ExcelStyles.CurrencyFormat;

        ws.Cell("A7").Value = "Yield";
        ws.Cell("B7").Value = (dividends?.Yield ?? 0m) / 100m;
        ws.Cell("B7").Style.NumberFormat.Format = ExcelStyles.PercentFormat;

        // Past dividends detail
        int pastCount = 0;
        if (dividends?.PastDividends is { Count: > 0 })
        {
            int row = 10;
            ws.Cell($"A{row}").Value = "Past Dividends";
            ws.Cell($"A{row}").Style.Font.Bold = true;
            row++;

            ws.Cell($"A{row}").Value = "Name";
            ws.Cell($"B{row}").Value = "Amount";
            ws.Cell($"C{row}").Value = "Date";
            ws.Cell($"D{row}").Value = "Type";
            ws.Cell($"E{row}").Value = "Category";
            ExcelStyles.ApplyHeaderStyle(ws.Row(row));
            row++;

            foreach (var div in dividends.PastDividends)
            {
                ws.Cell($"A{row}").Value = div.Asset?.Name ?? div.Holding?.Name ?? div.AssetType ?? "";
                ws.Cell($"B{row}").Value = div.Amount ?? 0m;
                ws.Cell($"B{row}").Style.NumberFormat.Format = ExcelStyles.CurrencyFormat;
                ws.Cell($"C{row}").Value = div.PaymentAt ?? "";
                ws.Cell($"D{row}").Value = div.AssetSubtype ?? "";
                ws.Cell($"E{row}").Value = div.AssetType ?? "";
                row++;
            }
            pastCount = dividends.PastDividends.Count;
        }

        // Upcoming dividends detail
        int upcomingCount = 0;
        if (dividends?.UpcomingDividends is { Count: > 0 })
        {
            int row = (dividends?.PastDividends?.Count ?? 0) + 14;
            ws.Cell($"A{row}").Value = "Upcoming Dividends";
            ws.Cell($"A{row}").Style.Font.Bold = true;
            row++;

            ws.Cell($"A{row}").Value = "Name";
            ws.Cell($"B{row}").Value = "Amount";
            ws.Cell($"C{row}").Value = "Date";
            ws.Cell($"D{row}").Value = "Status";
            ws.Cell($"E{row}").Value = "Category";
            ExcelStyles.ApplyHeaderStyle(ws.Row(row));
            row++;

            foreach (var div in dividends!.UpcomingDividends)
            {
                ws.Cell($"A{row}").Value = div.Asset?.Name ?? div.Holding?.Name ?? div.AssetType ?? "";
                ws.Cell($"B{row}").Value = div.Amount ?? 0m;
                ws.Cell($"B{row}").Style.NumberFormat.Format = ExcelStyles.CurrencyFormat;
                ws.Cell($"C{row}").Value = div.PaymentAt ?? "";
                ws.Cell($"D{row}").Value = div.Status ?? "";
                ws.Cell($"E{row}").Value = div.AssetType ?? "";
                row++;
            }
            upcomingCount = dividends.UpcomingDividends.Count;
        }

        ExcelStyles.FinalizeSheet(ws, 3);
        logger.LogInformation("    ✓ Past ({PastCount}), Upcoming ({UpcomingCount})", pastCount, upcomingCount);
    }
}
