namespace DotnetMcp.Abstractions;

public class DotnetMcpOptions
{
    public McpExposureMode ExposureMode { get; set; } = McpExposureMode.OptIn;

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

    /// <summary>
    /// MCP transport. Defaults to <see cref="McpTransportMode.Http"/>.
    /// Set to <see cref="McpTransportMode.Stdio"/> for local agent/CLI hosts.
    /// </summary>
    public McpTransportMode Transport { get; set; } = McpTransportMode.Http;

    /// <summary>
    /// When true, exposes a generated OpenAPI 3 document as an MCP resource.
    /// </summary>
    public bool EnableOpenApiResource { get; set; } = true;

    /// <summary>
    /// URI of the OpenAPI MCP resource.
    /// </summary>
    public string OpenApiResourceUri { get; set; } = "openapi://dotnet-mcp/document";

    /// <summary>
    /// Display name of the OpenAPI MCP resource.
    /// </summary>
    public string OpenApiResourceName { get; set; } = "OpenAPI Document";

    /// <summary>
    /// OpenAPI <c>info.title</c>.
    /// </summary>
    public string OpenApiTitle { get; set; } = "dotnet-mcp";

    /// <summary>
    /// OpenAPI <c>info.version</c>.
    /// </summary>
    public string OpenApiVersion { get; set; } = "1.0.0";
}
