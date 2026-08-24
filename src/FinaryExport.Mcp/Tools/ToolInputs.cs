using FinaryExport.Api;

namespace FinaryExport.Mcp.Tools;

internal static class ToolInputs
{
	public const int DefaultPageSize = 100;
	public const int MaximumPageSize = 500;

	public static string Period(string period)
	{
		try
		{
			return FinaryPeriod.Validate(period);
		}
		catch (ArgumentException ex)
		{
			throw McpErrors.InvalidInput(ex.Message);
		}
	}

	public static (int Offset, int Limit) Page(int offset, int limit)
	{
		if (offset < 0)
			throw McpErrors.InvalidInput("Offset must be zero or greater.");
		if (limit is < 1 or > MaximumPageSize)
			throw McpErrors.InvalidInput(
				$"Limit must be between 1 and {MaximumPageSize}.");

		return (offset, limit);
	}

	public static string ValueType(string valueType)
	{
		try
		{
			return FinaryValueType.Validate(valueType);
		}
		catch (ArgumentException ex)
		{
			throw McpErrors.InvalidInput(ex.Message);
		}
	}
}
