using DotnetMcp.Abstractions;

namespace DotnetMcp;

public sealed class EndpointDiscoveryService : IEndpointDiscoveryService
{
    private readonly McpToolCatalog _toolCatalog;

    public EndpointDiscoveryService(McpToolCatalog toolCatalog)
    {
        _toolCatalog = toolCatalog;
    }

    public IReadOnlyList<EndpointDescriptor> DiscoverEndpoints()
    {
        return _toolCatalog.GetTools()
            .Select(tool => tool.Descriptor)
            .ToList();
    }
}
