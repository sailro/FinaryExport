using ClosedXML.Excel;
using FinaryExport.Api;
using FinaryExport.Export.Formatting;
using FinaryExport.Models;

namespace FinaryExport.Export.Sheets;

// Writes a single Transactions sheet with all transactions across categories.
public sealed class TransactionsSheet : ISheetWriter
{
    public string SheetName => "Transactions";

    public async Task WriteAsync(IXLWorkbook workbook, IFinaryApiClient api, CancellationToken ct)
    {
        var ws = workbook.Worksheets.Add(SheetName);

        // Headers
        ws.Cell("A1").Value = "Category";
        ws.Cell("B1").Value = "Date";
        ws.Cell("C1").Value = "Name";
        ws.Cell("D1").Value = "Value";
        ws.Cell("E1").Value = "Type";
        ws.Cell("F1").Value = "Account";
        ws.Cell("G1").Value = "Institution";
        ws.Cell("H1").Value = "Currency";
        ws.Cell("I1").Value = "Commission";
        ExcelStyles.ApplyHeaderStyle(ws.Row(1));

        int row = 2;
        int totalRecords = 0;

        // Query all categories — some may have no transactions
        foreach (var category in Enum.GetValues<AssetCategory>())
        {
            try
            {
                var transactions = await api.GetCategoryTransactionsAsync(category, ct: ct);
                foreach (var tx in transactions)
                {
                    ws.Cell($"A{row}").Value = category.ToDisplayName();
                    ws.Cell($"B{row}").Value = tx.Date ?? "";
                    ws.Cell($"C{row}").Value = tx.DisplayName ?? tx.Name ?? "";
                    ws.Cell($"D{row}").Value = tx.Value ?? 0m;
                    ws.Cell($"D{row}").Style.NumberFormat.Format = ExcelStyles.CurrencyFormat;
                    ws.Cell($"E{row}").Value = tx.TransactionType ?? "";
                    ws.Cell($"F{row}").Value = tx.Account?.Name ?? "";
                    ws.Cell($"G{row}").Value = tx.Institution?.Name ?? "";
                    ws.Cell($"H{row}").Value = tx.Currency?.Code ?? "";
                    ws.Cell($"I{row}").Value = tx.Commission ?? 0m;
                    ws.Cell($"I{row}").Style.NumberFormat.Format = ExcelStyles.CurrencyFormat;
                    row++;
                    totalRecords++;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // Skip this category's transactions
            }
        }

        if (totalRecords == 0)
        {
            ws.Cell("A2").Value = "No transactions found";
        }

        ExcelStyles.FinalizeSheet(ws);
    }
}
