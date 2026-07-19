using DotnetMcp.Abstractions;
using Microsoft.AspNetCore.Builder;

namespace DotnetMcp.AspNetCore;

public static class DotnetMcpEndpointRouteBuilderExtensions
{
    public static TBuilder WithMcpExpose<TBuilder>(
        this TBuilder builder,
        string? name = null,
        string? description = null,
        bool readOnly = false,
        bool destructive = false)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new McpExposeAttribute
        {
            Name = name,
            Description = description,
            ReadOnly = readOnly,
            Destructive = destructive
        });

        return builder;
    }
}
