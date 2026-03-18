using ClosedXML.Excel;
using FinaryExport.Api;
using FinaryExport.Export.Formatting;
using FinaryExport.Models;
using Microsoft.Extensions.Logging;

namespace FinaryExport.Export.Sheets;

// Exports individual crypto positions from crypto accounts with full position details.
public sealed class CryptoHoldingsSheet(ILogger<CryptoHoldingsSheet> logger) : ISheetWriter
{
	public string SheetName => "Crypto Holdings";

	public async Task WriteAsync(IXLWorkbook workbook, IFinaryApiClient api, ExportContext context, CancellationToken ct)
	{
		var accounts = await api.GetCategoryAccountsAsync(AssetCategory.Cryptos, ct: ct);

		var ws = workbook.Worksheets.Add(SheetName);
		var currencyFormat = context.CurrencyFormat;

		// Headers
		ws.Cell("A1").Value = "Account";
		ws.Cell("B1").Value = "Name";
		ws.Cell("C1").Value = "Code";
		ws.Cell("D1").Value = "Quantity";
		ws.Cell("E1").Value = "Buy Price";
		ws.Cell("F1").Value = "Current Price";
		ws.Cell("G1").Value = "Value";
		ws.Cell("H1").Value = "Buy Value";
		ws.Cell("I1").Value = "+/- Value";
		ws.Cell("J1").Value = "+/- %";
		ExcelStyles.ApplyHeaderStyle(ws.Row(1));

		// Flatten accounts -> crypto positions, sorted by account name then crypto name
		var cryptoRows = accounts
		.Where(a => a.Cryptos is not null)
		.SelectMany(a => a.Cryptos!.Select(c => (
			Account: a.Name ?? "",
			Name: c.Asset?.Name ?? "",
			Code: c.Asset?.Code ?? "",
			Quantity: c.Quantity ?? 0m,
			BuyPrice: context.ResolveValue(c.DisplayBuyingPrice, c.BuyingPrice),
			CurrentPrice: context.ResolveValue(c.DisplayCurrentPrice, c.CurrentPrice),
			Value: context.ResolveValue(c.DisplayCurrentValue, c.CurrentValue),
			BuyValue: context.ResolveValue(c.DisplayBuyingValue, c.BuyingValue),
			PnlValue: context.ResolveValue(c.DisplayUnrealizedPnl, c.UnrealizedPnl),
			PnlPercent: (c.UnrealizedPnlPercent ?? 0m) / 100m
		)));

		// Flatten accounts -> fiat positions (EUR/USD cash held in crypto accounts)
		var fiatRows = accounts
		.Where(a => a.Fiats is not null)
		.SelectMany(a => a.Fiats!.Select(f => (
			Account: a.Name ?? "",
			Name: f.Asset?.Name ?? "",
			Code: f.Asset?.Code ?? "",
			Quantity: f.Quantity ?? 0m,
			BuyPrice: context.ResolveValue(f.DisplayCurrentPrice, f.CurrentPrice),
			CurrentPrice: context.ResolveValue(f.DisplayCurrentPrice, f.CurrentPrice),
			Value: context.ResolveValue(f.DisplayCurrentValue, f.CurrentValue),
			BuyValue: context.ResolveValue(f.DisplayCurrentValue, f.CurrentValue),
			PnlValue: context.ResolveValue(f.DisplayUnrealizedPnl, f.UnrealizedPnl),
			PnlPercent: (f.UnrealizedPnlPercent ?? 0m) / 100m
		)));

		var rows = cryptoRows.Concat(fiatRows)
		.OrderBy(r => r.Account)
		.ThenBy(r => r.Name)
		.ToList();

		var row = 2;
		foreach (var pos in rows)
		{
			ws.Cell($"A{row}").Value = pos.Account;
			ws.Cell($"B{row}").Value = pos.Name;
			ws.Cell($"C{row}").Value = pos.Code;

			ws.Cell($"D{row}").Value = pos.Quantity;
			ws.Cell($"D{row}").Style.NumberFormat.Format = "#,##0.########";

			ws.Cell($"E{row}").Value = pos.BuyPrice;
			ws.Cell($"E{row}").Style.NumberFormat.Format = currencyFormat;

			ws.Cell($"F{row}").Value = pos.CurrentPrice;
			ws.Cell($"F{row}").Style.NumberFormat.Format = currencyFormat;

			ws.Cell($"G{row}").Value = pos.Value;
			ws.Cell($"G{row}").Style.NumberFormat.Format = currencyFormat;

			ws.Cell($"H{row}").Value = pos.BuyValue;
			ws.Cell($"H{row}").Style.NumberFormat.Format = currencyFormat;

			ws.Cell($"I{row}").Value = pos.PnlValue;
			ws.Cell($"I{row}").Style.NumberFormat.Format = currencyFormat;

			ws.Cell($"J{row}").Value = pos.PnlPercent;
			ws.Cell($"J{row}").Style.NumberFormat.Format = ExcelStyles.PercentFormat;

			row++;
		}

		ExcelStyles.FinalizeSheet(ws);
		var totalPositions = rows.Count;
		var accountCount = accounts.Count(a => (a.Cryptos?.Count ?? 0) > 0 || (a.Fiats?.Count ?? 0) > 0);
		logger.LogInformation("    ✓ {PositionCount} positions (crypto + fiat) across {AccountCount} accounts", totalPositions, accountCount);
	}
}
