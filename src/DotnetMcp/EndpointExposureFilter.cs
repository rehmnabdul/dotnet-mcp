using DotnetMcp.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace DotnetMcp;

public sealed class EndpointExposureFilter
{
    private readonly DotnetMcpOptions _options;

    public EndpointExposureFilter(DotnetMcpOptions options)
    {
        _options = options;
    }

    public bool ShouldExpose(ApiDescription apiDescription)
    {
        if (IsExcludedPath(apiDescription.RelativePath))
        {
            return false;
        }

        if (ApiDescriptionMetadata.HasIgnoreAttribute(apiDescription))
        {
            return false;
        }

        var explicitlyAnnotated = IsExplicitlyAnnotated(apiDescription);
        var exposed = _options.ExposureMode switch
        {
            McpExposureMode.OptIn => explicitlyAnnotated,
            McpExposureMode.OptOut when _options.RequireExplicitAnnotation => explicitlyAnnotated,
            McpExposureMode.OptOut => true,
            _ => false
        };

        if (!exposed)
        {
            return false;
        }

        return _options.Filter?.Invoke(CreatePreviewDescriptor(apiDescription)) ?? true;
    }

    private bool IsExplicitlyAnnotated(ApiDescription apiDescription)
    {
        if (ApiDescriptionMetadata.GetExposeAttribute(apiDescription) is not null)
        {
            return true;
        }

        return ApiDescriptionMetadata.HasExposeAllAttribute(apiDescription);
    }

    private bool IsExcludedPath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || _options.ExcludePaths.Count == 0)
        {
            return false;
        }

        var normalizedPath = NormalizePath(relativePath);
        foreach (var excludedPath in _options.ExcludePaths)
        {
            if (string.IsNullOrWhiteSpace(excludedPath))
            {
                continue;
            }

            var normalizedExcludedPath = NormalizePath(excludedPath);
            if (normalizedPath.StartsWith(normalizedExcludedPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private EndpointDescriptor CreatePreviewDescriptor(ApiDescription apiDescription)
    {
        var actionDescriptor = ApiDescriptionMetadata.GetControllerActionDescriptor(apiDescription);
        var exposeAttribute = ApiDescriptionMetadata.GetExposeAttribute(apiDescription);

        return new EndpointDescriptor
        {
            Route = apiDescription.RelativePath ?? string.Empty,
            HttpMethod = apiDescription.HttpMethod ?? string.Empty,
            ToolName = string.Empty,
            Description = exposeAttribute?.Description,
            ReadOnly = exposeAttribute?.ReadOnly ?? IsReadOnlyHttpMethod(apiDescription.HttpMethod),
            Destructive = exposeAttribute?.Destructive ?? IsDestructiveHttpMethod(apiDescription.HttpMethod),
            Roles = exposeAttribute?.Roles ?? Array.Empty<string>(),
            ControllerName = actionDescriptor?.ControllerName,
            ActionName = actionDescriptor?.ActionName,
            DeclaringType = ApiDescriptionMetadata.GetControllerType(apiDescription)
        };
    }

    private static string NormalizePath(string path)
    {
        return path.Trim().TrimStart('/');
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
