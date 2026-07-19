using System.Text.Json;
using DotnetMcp.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace DotnetMcp;

public sealed class McpToolEntry
{
    public EndpointDescriptor Descriptor { get; init; } = new();

    public ApiDescription ApiDescription { get; init; } = null!;

    public JsonElement InputSchema { get; init; }
}
