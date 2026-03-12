using ClosedXML.Excel;
using FinaryExport.Api;
using FinaryExport.Export.Formatting;
using FinaryExport.Models;

namespace FinaryExport.Export.Sheets;

// Exports individual securities from investment accounts with full position details.
public sealed class HoldingsSheet : ISheetWriter
{
    public string SheetName => "Holdings";

    public async Task WriteAsync(IXLWorkbook workbook, IFinaryApiClient api, ExportContext context, CancellationToken ct)
    {
        var accounts = await api.GetCategoryAccountsAsync(AssetCategory.Investments, ct);

        var ws = workbook.Worksheets.Add(SheetName);

        // Headers
        ws.Cell("A1").Value = "Account";
        ws.Cell("B1").Value = "Name";
        ws.Cell("C1").Value = "ISIN";
        ws.Cell("D1").Value = "Symbol";
        ws.Cell("E1").Value = "Type";
        ws.Cell("F1").Value = "Quantity";
        ws.Cell("G1").Value = "Buy Price";
        ws.Cell("H1").Value = "Current Price";
        ws.Cell("I1").Value = "Value";
        ws.Cell("J1").Value = "+/- Value";
        ws.Cell("K1").Value = "+/- %";
        ExcelStyles.ApplyHeaderStyle(ws.Row(1));

        // Flatten accounts -> securities, sorted by account name then security name
        var rows = accounts
            .Where(a => a.Securities is not null)
            .SelectMany(a => a.Securities!.Select(s => (Account: a, Position: s)))
            .OrderBy(r => r.Account.Name ?? "")
            .ThenBy(r => r.Position.Security?.Name ?? "")
            .ToList();

        int row = 2;
        foreach (var (account, pos) in rows)
        {
            var sec = pos.Security;

            ws.Cell($"A{row}").Value = account.Name ?? "";
            ws.Cell($"B{row}").Value = sec?.Name ?? "";
            ws.Cell($"C{row}").Value = sec?.Isin ?? "";
            ws.Cell($"D{row}").Value = sec?.Symbol ?? "";
            ws.Cell($"E{row}").Value = sec?.SecurityType ?? "";

            ws.Cell($"F{row}").Value = pos.Quantity ?? 0m;
            ws.Cell($"F{row}").Style.NumberFormat.Format = "#,##0.####";

            ws.Cell($"G{row}").Value = context.ResolveValue(pos.DisplayBuyingPrice, pos.BuyingPrice);
            ws.Cell($"G{row}").Style.NumberFormat.Format = ExcelStyles.CurrencyFormat;

            ws.Cell($"H{row}").Value = sec?.CurrentPrice ?? 0m;
            ws.Cell($"H{row}").Style.NumberFormat.Format = ExcelStyles.CurrencyFormat;

            ws.Cell($"I{row}").Value = context.ResolveValue(pos.DisplayCurrentValue, pos.CurrentValue);
            ws.Cell($"I{row}").Style.NumberFormat.Format = ExcelStyles.CurrencyFormat;

            ws.Cell($"J{row}").Value = context.ResolveValue(pos.DisplayCurrentUpnl, pos.CurrentUpnl);
            ws.Cell($"J{row}").Style.NumberFormat.Format = ExcelStyles.CurrencyFormat;

            var pnlPct = context.ResolveValue(pos.DisplayCurrentUpnlPercent, pos.CurrentUpnlPercent) / 100m;
            ws.Cell($"K{row}").Value = pnlPct;
            ws.Cell($"K{row}").Style.NumberFormat.Format = ExcelStyles.PercentFormat;

            row++;
        }

        ExcelStyles.FinalizeSheet(ws);
    }
}
