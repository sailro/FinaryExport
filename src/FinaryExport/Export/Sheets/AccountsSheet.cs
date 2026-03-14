using ClosedXML.Excel;
using FinaryExport.Api;
using FinaryExport.Export.Formatting;
using FinaryExport.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinaryExport.Export.Sheets;

// Writes one sheet per asset category with account data.
public sealed class AccountsSheet(ILogger<AccountsSheet> logger) : ISheetWriter
{
    public string SheetName => "Accounts";

    public async Task WriteAsync(IXLWorkbook workbook, IFinaryApiClient api, ExportContext context, CancellationToken ct)
    {
        foreach (var category in Enum.GetValues<AssetCategory>())
        {
            try
            {
                var accounts = await api.GetCategoryAccountsAsync(category, ct);
                if (accounts.Count == 0) continue;

                var sheetName = category.ToDisplayName();
                // Excel sheet names max 31 chars
                if (sheetName.Length > 31) sheetName = sheetName[..31];
                var ws = workbook.Worksheets.Add(sheetName);

                // Headers
                ws.Cell("A1").Value = "Name";
                ws.Cell("B1").Value = "Institution";
                ws.Cell("C1").Value = "Balance";
                ws.Cell("D1").Value = "Currency";
                ws.Cell("E1").Value = "Buying Value";
                ws.Cell("F1").Value = "Unrealized P&L";
                ws.Cell("G1").Value = "Annual Yield";
                ws.Cell("H1").Value = "IBAN";
                ws.Cell("I1").Value = "Opened At";
                ws.Cell("J1").Value = "Last Sync";
                ExcelStyles.ApplyHeaderStyle(ws.Row(1));

                var row = 2;
                foreach (var account in accounts)
                {
                    ws.Cell($"A{row}").Value = account.Name ?? "";
                    ws.Cell($"B{row}").Value = account.Institution?.Name ?? "";
                    ws.Cell($"C{row}").Value = context.ResolveValue(account.DisplayBalance, account.Balance);
                    ws.Cell($"C{row}").Style.NumberFormat.Format = ExcelStyles.CurrencyFormat;
                    ws.Cell($"D{row}").Value = account.Currency?.Code ?? "";
                    ws.Cell($"E{row}").Value = context.ResolveValue(account.DisplayBuyingValue, account.BuyingValue);
                    ws.Cell($"E{row}").Style.NumberFormat.Format = ExcelStyles.CurrencyFormat;
                    ws.Cell($"F{row}").Value = account.UnrealizedPnl ?? 0m;
                    ws.Cell($"F{row}").Style.NumberFormat.Format = ExcelStyles.CurrencyFormat;
                    ws.Cell($"G{row}").Value = (account.AnnualYield ?? 0m) / 100m;
                    ws.Cell($"G{row}").Style.NumberFormat.Format = ExcelStyles.PercentFormat;
                    ws.Cell($"H{row}").Value = account.Iban ?? "";
                    ws.Cell($"I{row}").Value = account.OpenedAt?.ToString("yyyy-MM-dd") ?? "";
                    ws.Cell($"J{row}").Value = account.LastSyncAt?.ToString("yyyy-MM-dd HH:mm") ?? "";
                    row++;
                }

                ExcelStyles.FinalizeSheet(ws);
                logger.LogInformation("    ✓ {SheetName} ({AccountCount} accounts)", sheetName, accounts.Count);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to export accounts for category {Category}", category);
            }
        }
    }
}
