using System.Text.Json;

namespace FinaryExport.Models.Portfolio;

public sealed record TimeseriesData
{
    public string? Label { get; init; }
    public decimal? PeriodEvolutionPercent { get; init; }

    // API returns array of [date_string, value] tuples (not objects)
    public JsonElement? Timeseries { get; init; }

    public decimal? PeriodEvolution { get; init; }
    public decimal? DisplayAmount { get; init; }
    public decimal? DisplayValueDifference { get; init; }
    public decimal? DisplayValueEvolution { get; init; }
    public decimal? Balance { get; init; }
}
