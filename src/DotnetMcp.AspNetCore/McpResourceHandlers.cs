using DotnetMcp;
using DotnetMcp.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DotnetMcp.AspNetCore;

internal sealed class McpResourceHandlers
{
    private readonly OpenApiDocumentGenerator _openApiDocumentGenerator;
    private readonly DotnetMcpOptions _options;

    public McpResourceHandlers(
        OpenApiDocumentGenerator openApiDocumentGenerator,
        IOptions<DotnetMcpOptions> options)
    {
        _openApiDocumentGenerator = openApiDocumentGenerator;
        _options = options.Value;
    }

    public ValueTask<ListResourcesResult> ListResourcesAsync(
        RequestContext<ListResourcesRequestParams> context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.EnableOpenApiResource)
        {
            return ValueTask.FromResult(new ListResourcesResult
            {
                Resources = []
            });
        }

        return ValueTask.FromResult(new ListResourcesResult
        {
            Resources =
            [
                new Resource
                {
                    Uri = _options.OpenApiResourceUri,
                    Name = _options.OpenApiResourceName,
                    Description = "OpenAPI 3 document for MCP-exposed endpoints.",
                    MimeType = "application/json"
                }
            ]
        });
    }

    public ValueTask<ReadResourceResult> ReadResourceAsync(
        RequestContext<ReadResourceRequestParams> context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var uri = context.Params?.Uri;
        if (string.IsNullOrWhiteSpace(uri))
        {
            throw new McpException("Resource URI is required.", McpErrorCode.InvalidParams);
        }

        if (!_options.EnableOpenApiResource ||
            !string.Equals(uri, _options.OpenApiResourceUri, StringComparison.Ordinal))
        {
            throw new McpException($"Resource '{uri}' was not found.", McpErrorCode.InvalidParams);
        }

        var document = _openApiDocumentGenerator.GenerateJson();

        return ValueTask.FromResult(new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = uri,
                    MimeType = "application/json",
                    Text = document
                }
            ]
        });
    }
}
