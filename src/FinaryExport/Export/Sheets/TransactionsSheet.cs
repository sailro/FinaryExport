using ClosedXML.Excel;
using FinaryExport.Api;
using FinaryExport.Export.Formatting;
using FinaryExport.Models;
using Microsoft.Extensions.Logging;

namespace FinaryExport.Export.Sheets;

// Writes a single Transactions sheet with all transactions across categories.
public sealed class TransactionsSheet(ILogger<TransactionsSheet> logger) : ISheetWriter
{
public string SheetName => "Transactions";

public async Task WriteAsync(IXLWorkbook workbook, IFinaryApiClient api, ExportContext context, CancellationToken ct)
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

var row = 2;
var totalRecords = 0;
var cutoff = PeriodHelper.GetCutoffDate(context.Period);

foreach (var category in Enum.GetValues<AssetCategory>().Where(c => c.HasTransactions()))
{
try
{
var transactions = await api.GetCategoryTransactionsAsync(category, period: context.Period, ct: ct);
var categoryRecords = 0;
foreach (var tx in transactions.Where(t => PeriodHelper.IsOnOrAfter(t.Date, cutoff)))
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
categoryRecords++;
}
if (categoryRecords > 0)
{
logger.LogInformation("    ✓ {Category} ({RecordCount} transactions)", category.ToDisplayName(), categoryRecords);
}
}
catch (OperationCanceledException) when (ct.IsCancellationRequested)
{
throw;
}
catch (Exception ex)
{
logger.LogWarning(ex, "Failed to export transactions for category {Category}", category);
}
}

if (totalRecords == 0)
{
ws.Cell("A2").Value = "No transactions found";
}

ExcelStyles.FinalizeSheet(ws);
}
}
