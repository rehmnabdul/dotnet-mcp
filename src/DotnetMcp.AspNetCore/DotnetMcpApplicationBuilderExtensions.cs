using DotnetMcp.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DotnetMcp.AspNetCore;

public static class DotnetMcpApplicationBuilderExtensions
{
    /// <summary>
    /// Maps the MCP HTTP endpoint. No-op when <see cref="DotnetMcpOptions.Transport"/> is
    /// <see cref="McpTransportMode.Stdio"/> (stdio is hosted via <c>WithStdioServerTransport</c>).
    /// </summary>
    public static WebApplication MapDotnetMcp(this WebApplication app, string pattern = "/mcp")
    {
        if (IsStdioTransport(app.Services))
        {
            return app;
        }

        app.MapMcp(pattern);
        return app;
    }

    /// <summary>
    /// Maps the MCP HTTP endpoint. No-op when transport is stdio.
    /// </summary>
    public static IEndpointRouteBuilder MapDotnetMcp(this IEndpointRouteBuilder endpoints, string pattern = "/mcp")
    {
        if (IsStdioTransport(endpoints.ServiceProvider))
        {
            return endpoints;
        }

        endpoints.MapMcp(pattern);
        return endpoints;
    }

    private static bool IsStdioTransport(IServiceProvider services)
    {
        var options = services.GetService<IOptions<DotnetMcpOptions>>()?.Value;
        return options?.Transport == McpTransportMode.Stdio;
    }
}
