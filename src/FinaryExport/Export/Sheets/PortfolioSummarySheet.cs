using ClosedXML.Excel;
using FinaryExport.Api;
using FinaryExport.Export.Formatting;
using FinaryExport.Models;

namespace FinaryExport.Export.Sheets;

public sealed class PortfolioSummarySheet : ISheetWriter
{
    public string SheetName => "Summary";

    public async Task WriteAsync(IXLWorkbook workbook, IFinaryApiClient api, ExportContext context, CancellationToken ct)
    {
        var portfolio = await api.GetPortfolioAsync(ct: ct);
        var ws = workbook.Worksheets.Add(SheetName);

        // Header
        ws.Cell("A1").Value = "Portfolio Summary";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 14;

        // Gross values
        ws.Cell("A3").Value = "Metric";
        ws.Cell("B3").Value = "Value";
        ExcelStyles.ApplyHeaderStyle(ws.Row(3));

        int row = 4;
        if (portfolio?.Gross?.Total is not null)
        {
            var gross = portfolio.Gross.Total;
            ws.Cell($"A{row}").Value = "Gross Total";
            ws.Cell($"B{row}").Value = context.ResolveValue(gross.DisplayAmount, gross.Amount);
            ws.Cell($"B{row}").Style.NumberFormat.Format = ExcelStyles.CurrencyFormat;
            row++;

            ws.Cell($"A{row}").Value = "Gross Evolution";
            ws.Cell($"B{row}").Value = gross.Evolution ?? 0m;
            ws.Cell($"B{row}").Style.NumberFormat.Format = ExcelStyles.CurrencyFormat;
            row++;

            ws.Cell($"A{row}").Value = "Gross Evolution %";
            ws.Cell($"B{row}").Value = (gross.EvolutionPercent ?? 0m) / 100m;
            ws.Cell($"B{row}").Style.NumberFormat.Format = ExcelStyles.PercentFormat;
            row++;
        }

        if (portfolio?.Net?.Total is not null)
        {
            var net = portfolio.Net.Total;
            ws.Cell($"A{row}").Value = "Net Total";
            ws.Cell($"B{row}").Value = context.ResolveValue(net.DisplayAmount, net.Amount);
            ws.Cell($"B{row}").Style.NumberFormat.Format = ExcelStyles.CurrencyFormat;
            row++;

            ws.Cell($"A{row}").Value = "Net Evolution";
            ws.Cell($"B{row}").Value = net.Evolution ?? 0m;
            ws.Cell($"B{row}").Style.NumberFormat.Format = ExcelStyles.CurrencyFormat;
            row++;

            ws.Cell($"A{row}").Value = "Net Evolution %";
            ws.Cell($"B{row}").Value = (net.EvolutionPercent ?? 0m) / 100m;
            ws.Cell($"B{row}").Style.NumberFormat.Format = ExcelStyles.PercentFormat;
            row++;
        }

        // Per-category breakdown
        row += 2;
        ws.Cell($"A{row}").Value = "Category";
        ws.Cell($"B{row}").Value = "Accounts";
        ws.Cell($"C{row}").Value = "Total Balance";
        ExcelStyles.ApplyHeaderStyle(ws.Row(row));
        row++;

        foreach (var category in Enum.GetValues<Models.AssetCategory>())
        {
            try
            {
                var accounts = await api.GetCategoryAccountsAsync(category, ct);
                var totalBalance = accounts.Sum(a => context.ResolveValue(a.DisplayBalance, a.Balance));
                ws.Cell($"A{row}").Value = category.ToDisplayName();
                ws.Cell($"B{row}").Value = accounts.Count;
                ws.Cell($"C{row}").Value = totalBalance;
                ws.Cell($"C{row}").Style.NumberFormat.Format = ExcelStyles.CurrencyFormat;
                row++;
            }
            catch (Exception)
            {
                ws.Cell($"A{row}").Value = category.ToDisplayName();
                ws.Cell($"B{row}").Value = "Error";
                row++;
            }
        }

        ExcelStyles.FinalizeSheet(ws, 3);
    }
}
