namespace DotnetMcp.Abstractions;

/// <summary>
/// Transport used by the MCP server hosted through DotnetMcp.
/// </summary>
public enum McpTransportMode
{
    /// <summary>
    /// Streamable HTTP / SSE endpoint mapped with <c>MapDotnetMcp</c>.
    /// </summary>
    Http = 0,

    /// <summary>
    /// Local process transport over stdin/stdout (typical for IDE / CLI MCP hosts).
    /// </summary>
    Stdio = 1
}
