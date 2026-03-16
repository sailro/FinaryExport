namespace FinaryExport.Models.Transactions;

public sealed record TransactionCategory
{
	public int Id { get; init; }
	public string? Name { get; init; }
	public string? Color { get; init; }
	public string? Icon { get; init; }
	public bool IsCustom { get; init; }
	public int? MainCategoryId { get; init; }
	public decimal? Target { get; init; }
	public bool? ShouldReachTarget { get; init; }
	public bool IsSubcategory { get; init; }
	public List<TransactionCategory>? Subcategories { get; init; }
}
