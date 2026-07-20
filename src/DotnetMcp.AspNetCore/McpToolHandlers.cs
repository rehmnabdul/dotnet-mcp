using System.Text.Json;
using DotnetMcp;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DotnetMcp.AspNetCore;

internal sealed class McpToolHandlers
{
    private readonly McpToolCatalog _toolCatalog;
    private readonly McpEndpointInvoker _endpointInvoker;

    public McpToolHandlers(McpToolCatalog toolCatalog, McpEndpointInvoker endpointInvoker)
    {
        _toolCatalog = toolCatalog;
        _endpointInvoker = endpointInvoker;
    }

    public ValueTask<ListToolsResult> ListToolsAsync(
        RequestContext<ListToolsRequestParams> context,
        CancellationToken cancellationToken)
    {
        var tools = _toolCatalog.GetTools()
            .Select(entry => new Tool
            {
                Name = entry.Descriptor.ToolName,
                Description = entry.Descriptor.Description,
                InputSchema = entry.InputSchema,
                Annotations = new ToolAnnotations
                {
                    ReadOnlyHint = entry.Descriptor.ReadOnly,
                    DestructiveHint = entry.Descriptor.Destructive,
                    Title = entry.Descriptor.ActionName ?? entry.Descriptor.ToolName
                }
            })
            .ToList();

        return ValueTask.FromResult(new ListToolsResult
        {
            Tools = tools
        });
    }

    public async ValueTask<CallToolResult> CallToolAsync(
        RequestContext<CallToolRequestParams> context,
        CancellationToken cancellationToken)
    {
        var toolName = context.Params?.Name;
        if (string.IsNullOrWhiteSpace(toolName))
        {
            throw new McpException("Tool name is required.");
        }

        var tool = _toolCatalog.FindByName(toolName);
        if (tool is null)
        {
            throw new McpException($"Tool '{toolName}' was not found.");
        }

        var requestServices = context.Services ?? throw new InvalidOperationException("Server services are unavailable.");
        var httpContext = requestServices.GetService<IHttpContextAccessor>()?.HttpContext;

        IReadOnlyDictionary<string, JsonElement>? arguments = null;
        if (context.Params?.Arguments is { } rawArguments)
        {
            arguments = new Dictionary<string, JsonElement>(rawArguments);
        }

        return await _endpointInvoker.InvokeAsync(
            tool,
            arguments,
            httpContext,
            requestServices,
            cancellationToken);
    }
}
