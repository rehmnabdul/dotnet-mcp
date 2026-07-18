namespace DotnetMcp.Abstractions;

public interface IEndpointDiscoveryService
{
    IReadOnlyList<EndpointDescriptor> DiscoverEndpoints();
}
