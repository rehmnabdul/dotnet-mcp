using DotnetMcp.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace DotnetMcp;

public sealed class EndpointDiscoveryService : IEndpointDiscoveryService
{
    private readonly IApiDescriptionGroupCollectionProvider _apiDescriptionProvider;
    private readonly EndpointExposureFilter _exposureFilter;
    private readonly ToolNamingStrategy _namingStrategy;

    public EndpointDiscoveryService(
        IApiDescriptionGroupCollectionProvider apiDescriptionProvider,
        EndpointExposureFilter exposureFilter,
        ToolNamingStrategy namingStrategy)
    {
        _apiDescriptionProvider = apiDescriptionProvider;
        _exposureFilter = exposureFilter;
        _namingStrategy = namingStrategy;
    }

    public IReadOnlyList<EndpointDescriptor> DiscoverEndpoints()
    {
        var endpoints = new List<EndpointDescriptor>();

        foreach (var group in _apiDescriptionProvider.ApiDescriptionGroups.Items)
        {
            foreach (var apiDescription in group.Items)
            {
                if (!_exposureFilter.ShouldExpose(apiDescription))
                {
                    continue;
                }

                var exposeAttribute = ApiDescriptionMetadata.GetExposeAttribute(apiDescription);
                var actionDescriptor = ApiDescriptionMetadata.GetControllerActionDescriptor(apiDescription);

                endpoints.Add(new EndpointDescriptor
                {
                    Route = apiDescription.RelativePath ?? string.Empty,
                    HttpMethod = apiDescription.HttpMethod ?? string.Empty,
                    ToolName = _namingStrategy.GenerateToolName(apiDescription, exposeAttribute),
                    Description = exposeAttribute?.Description,
                    ReadOnly = exposeAttribute?.ReadOnly ?? IsReadOnlyHttpMethod(apiDescription.HttpMethod),
                    Destructive = exposeAttribute?.Destructive ?? IsDestructiveHttpMethod(apiDescription.HttpMethod),
                    Roles = exposeAttribute?.Roles ?? Array.Empty<string>(),
                    ControllerName = actionDescriptor?.ControllerName,
                    ActionName = actionDescriptor?.ActionName,
                    DeclaringType = ApiDescriptionMetadata.GetControllerType(apiDescription)
                });
            }
        }

        return endpoints;
    }

    private static bool IsReadOnlyHttpMethod(string? httpMethod)
    {
        return httpMethod is "GET" or "HEAD" or "OPTIONS";
    }

    private static bool IsDestructiveHttpMethod(string? httpMethod)
    {
        return httpMethod is "DELETE";
    }
}
