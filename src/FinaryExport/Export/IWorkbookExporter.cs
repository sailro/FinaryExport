namespace FinaryExport.Export;

public interface IWorkbookExporter
{
	Task ExportAsync(string outputPath, Api.IFinaryApiClient api, ExportContext? context = null, CancellationToken ct = default);
}
