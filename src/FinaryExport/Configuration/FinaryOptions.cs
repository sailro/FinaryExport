namespace FinaryExport.Configuration;

public sealed class FinaryOptions
{
	public const string SectionName = "Finary";

	public string OutputPath { get; set; } = "finary-export.xlsx";
	public string Period { get; set; } = "all";
	public string? SessionStorePath { get; set; }
}
