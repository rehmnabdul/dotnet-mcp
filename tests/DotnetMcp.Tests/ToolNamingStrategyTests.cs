using System.Reflection;
using DotnetMcp.Abstractions;
using DotnetMcp.Tests.Helpers;
using FluentAssertions;

namespace DotnetMcp.Tests;

public class ToolNamingStrategyTests
{
    [Fact]
    public void GenerateToolName_ConvertsRouteParametersToBySegments()
    {
        var options = new DotnetMcpOptions();
        var strategy = new ToolNamingStrategy(options);
        var apiDescription = ApiDescriptionTestHelper.Create(
            typeof(UnannotatedController),
            nameof(UnannotatedController.GetUsers),
            "GET",
            "api/users/{id}");

        var toolName = strategy.GenerateToolName(apiDescription);

        toolName.Should().Be("get_users_by_id");
    }

    [Fact]
    public void GenerateToolName_UsesExposeAttributeNameWhenProvided()
    {
        var options = new DotnetMcpOptions();
        var strategy = new ToolNamingStrategy(options);
        var apiDescription = ApiDescriptionTestHelper.Create(
            typeof(NamedToolController),
            nameof(NamedToolController.GetProfile),
            "GET",
            "api/profile");

        var exposeAttribute = typeof(NamedToolController)
            .GetMethod(nameof(NamedToolController.GetProfile))!
            .GetCustomAttribute<McpExposeAttribute>();

        var toolName = strategy.GenerateToolName(apiDescription, exposeAttribute);

        toolName.Should().Be("fetch_user_profile");
    }

    [Fact]
    public void GenerateToolName_AppliesPrefixAndOmitsHttpMethodWhenDisabled()
    {
        var options = new DotnetMcpOptions
        {
            MapHttpMethods = false,
            ToolNamePrefix = "acme"
        };
        var strategy = new ToolNamingStrategy(options);
        var apiDescription = ApiDescriptionTestHelper.Create(
            typeof(UnannotatedController),
            nameof(UnannotatedController.GetUsers),
            "GET",
            "api/users");

        var toolName = strategy.GenerateToolName(apiDescription);

        toolName.Should().Be("acme_users");
    }

    [Fact]
    public void GenerateToolName_StripsRouteConstraintsFromParameters()
    {
        var options = new DotnetMcpOptions();
        var strategy = new ToolNamingStrategy(options);
        var apiDescription = ApiDescriptionTestHelper.Create(
            typeof(UnannotatedController),
            nameof(UnannotatedController.GetUsers),
            "GET",
            "api/users/{id:int}");

        var toolName = strategy.GenerateToolName(apiDescription);

        toolName.Should().Be("get_users_by_id");
    }
}
