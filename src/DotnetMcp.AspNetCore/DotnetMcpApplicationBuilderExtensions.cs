using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace DotnetMcp.AspNetCore;

public static class DotnetMcpApplicationBuilderExtensions
{
    public static WebApplication MapDotnetMcp(this WebApplication app, string pattern = "/mcp")
    {
        app.MapMcp(pattern);
        return app;
    }

    public static IEndpointRouteBuilder MapDotnetMcp(this IEndpointRouteBuilder endpoints, string pattern = "/mcp")
    {
        endpoints.MapMcp(pattern);
        return endpoints;
    }
}
