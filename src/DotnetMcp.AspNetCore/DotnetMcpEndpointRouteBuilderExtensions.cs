using DotnetMcp.Abstractions;
using Microsoft.AspNetCore.Builder;

namespace DotnetMcp.AspNetCore;

public static class DotnetMcpEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Marks a Minimal API endpoint for MCP exposure.
    /// </summary>
    /// <param name="builder">Endpoint convention builder.</param>
    /// <param name="name">Explicit MCP tool name. When omitted, a name is generated from the route.</param>
    /// <param name="description">Tool description returned by <c>tools/list</c>.</param>
    /// <param name="readOnly">MCP read-only hint.</param>
    /// <param name="destructive">MCP destructive hint.</param>
    /// <param name="roles">Optional role names required for MCP tool calls.</param>
    public static TBuilder WithMcpExpose<TBuilder>(
        this TBuilder builder,
        string? name = null,
        string? description = null,
        bool readOnly = false,
        bool destructive = false,
        string[]? roles = null)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new McpExposeAttribute
        {
            Name = name,
            Description = description,
            ReadOnly = readOnly,
            Destructive = destructive,
            Roles = roles ?? Array.Empty<string>()
        });

        return builder;
    }
}
