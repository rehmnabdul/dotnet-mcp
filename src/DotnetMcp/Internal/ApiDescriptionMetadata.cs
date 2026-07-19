using System.Reflection;
using DotnetMcp.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace DotnetMcp;

internal static class ApiDescriptionMetadata
{
    public static ControllerActionDescriptor? GetControllerActionDescriptor(ApiDescription apiDescription)
    {
        return apiDescription.ActionDescriptor as ControllerActionDescriptor;
    }

    public static MethodInfo? GetMethodInfo(ApiDescription apiDescription)
    {
        return GetControllerActionDescriptor(apiDescription)?.MethodInfo;
    }

    public static Type? GetControllerType(ApiDescription apiDescription)
    {
        return GetControllerActionDescriptor(apiDescription)?.ControllerTypeInfo?.AsType();
    }

    public static McpExposeAttribute? GetExposeAttribute(ApiDescription apiDescription)
    {
        var fromMethod = GetMethodInfo(apiDescription)?.GetCustomAttribute<McpExposeAttribute>();
        if (fromMethod is not null)
        {
            return fromMethod;
        }

        return apiDescription.ActionDescriptor.EndpointMetadata
            .OfType<McpExposeAttribute>()
            .FirstOrDefault();
    }

    public static bool HasIgnoreAttribute(ApiDescription apiDescription)
    {
        var methodInfo = GetMethodInfo(apiDescription);
        if (methodInfo?.GetCustomAttribute<McpIgnoreAttribute>() is not null)
        {
            return true;
        }

        var controllerType = GetControllerType(apiDescription);
        if (controllerType?.GetCustomAttribute<McpIgnoreAttribute>() is not null)
        {
            return true;
        }

        return apiDescription.ActionDescriptor.EndpointMetadata
            .OfType<McpIgnoreAttribute>()
            .Any();
    }

    public static bool HasExposeAllAttribute(ApiDescription apiDescription)
    {
        var controllerType = GetControllerType(apiDescription);
        if (controllerType?.GetCustomAttribute<McpExposeAllAttribute>() is not null)
        {
            return true;
        }

        return apiDescription.ActionDescriptor.EndpointMetadata
            .OfType<McpExposeAllAttribute>()
            .Any();
    }
}
