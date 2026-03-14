using ClosedXML.Excel;
using FinaryExport.Api;

namespace FinaryExport.Export.Sheets;

public interface ISheetWriter
{
	string SheetName { get; }
	Task WriteAsync(IXLWorkbook workbook, IFinaryApiClient api, ExportContext context, CancellationToken ct);
}
