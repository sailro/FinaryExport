using ModelContextProtocol;

namespace FinaryExport.Mcp;

internal static class McpErrors
{
	public static McpException InvalidInput(string message) => new(message);

	public static McpException NotFound(string message) => new(message);
}
