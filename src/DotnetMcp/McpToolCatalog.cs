using DotnetMcp.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace DotnetMcp;

public sealed class McpToolCatalog
{
    private readonly IApiDescriptionGroupCollectionProvider _apiDescriptionProvider;
    private readonly EndpointExposureFilter _exposureFilter;
    private readonly ToolNamingStrategy _namingStrategy;
    private readonly ToolSchemaGenerator _schemaGenerator;

    public McpToolCatalog(
        IApiDescriptionGroupCollectionProvider apiDescriptionProvider,
        EndpointExposureFilter exposureFilter,
        ToolNamingStrategy namingStrategy,
        ToolSchemaGenerator schemaGenerator)
    {
        _apiDescriptionProvider = apiDescriptionProvider;
        _exposureFilter = exposureFilter;
        _namingStrategy = namingStrategy;
        _schemaGenerator = schemaGenerator;
    }

    public IReadOnlyList<McpToolEntry> GetTools()
    {
        var tools = new List<McpToolEntry>();

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

                tools.Add(new McpToolEntry
                {
                    ApiDescription = apiDescription,
                    InputSchema = _schemaGenerator.GenerateInputSchema(apiDescription),
                    Descriptor = new EndpointDescriptor
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
                    }
                });
            }
        }

        return tools;
    }

    public McpToolEntry? FindByName(string toolName)
    {
        return GetTools().FirstOrDefault(tool => string.Equals(tool.Descriptor.ToolName, toolName, StringComparison.Ordinal));
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
