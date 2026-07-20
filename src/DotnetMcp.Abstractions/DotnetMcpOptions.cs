namespace DotnetMcp.Abstractions;

public class DotnetMcpOptions
{
    public McpExposureMode ExposureMode { get; set; } = McpExposureMode.OptOut;

    public bool MapHttpMethods { get; set; } = true;

    public IReadOnlyList<string> ExcludePaths { get; set; } = Array.Empty<string>();

    public string ToolNamePrefix { get; set; } = string.Empty;

    public Func<EndpointDescriptor, bool>? Filter { get; set; }

    public bool RequireExplicitAnnotation { get; set; }

    public string McpRoutePattern { get; set; } = "/mcp";

    /// <summary>
    /// When true, MCP tool calls enforce ASP.NET Core authorization metadata
    /// (<c>[Authorize]</c>, policies, roles) and <see cref="McpExposeAttribute.Roles"/>.
    /// </summary>
    public bool EnforceEndpointAuthorization { get; set; } = true;
}
