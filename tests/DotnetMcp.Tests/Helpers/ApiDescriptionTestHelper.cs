using System.Reflection;
using DotnetMcp.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace DotnetMcp.Tests.Helpers;

internal static class ApiDescriptionTestHelper
{
    public static ApiDescription Create(
        Type controllerType,
        string methodName,
        string httpMethod,
        string relativePath)
    {
        var methodInfo = controllerType.GetMethod(methodName)
            ?? throw new InvalidOperationException($"Method '{methodName}' was not found on '{controllerType.Name}'.");

        var actionDescriptor = new ControllerActionDescriptor
        {
            ControllerTypeInfo = controllerType.GetTypeInfo(),
            MethodInfo = methodInfo,
            ControllerName = controllerType.Name.Replace("Controller", string.Empty),
            ActionName = methodName
        };

        return new ApiDescription
        {
            HttpMethod = httpMethod,
            RelativePath = relativePath,
            ActionDescriptor = actionDescriptor
        };
    }
}

internal class UnannotatedController
{
    public void GetUsers()
    {
    }

    public void DeleteUser()
    {
    }
}

[McpExposeAll]
internal class ExposeAllController
{
    public void GetItems()
    {
    }

    [McpIgnore]
    public void GetSecret()
    {
    }
}

internal class ExplicitController
{
    [McpExpose(Description = "Gets a user by id", ReadOnly = true, Roles = new[] { "admin" })]
    public void GetUser()
    {
    }

    public void UpdateUser()
    {
    }
}

[McpIgnore]
internal class IgnoredController
{
    public void GetBlocked()
    {
    }
}

internal class NamedToolController
{
    [McpExpose(Name = "fetch_user_profile")]
    public void GetProfile()
    {
    }
}
