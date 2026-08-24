namespace FinaryExport.Api;

public static class FinaryValueType
{
	public static string Validate(string valueType)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(valueType);
		var normalized = valueType.Trim().ToLowerInvariant();
		if (normalized is not ("gross" or "net"))
			throw new ArgumentException("Value type must be gross or net.", nameof(valueType));

		return normalized;
	}
}
