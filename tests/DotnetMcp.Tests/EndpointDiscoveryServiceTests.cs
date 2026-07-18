using DotnetMcp.Abstractions;
using DotnetMcp.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace DotnetMcp.Tests;

public class EndpointDiscoveryServiceTests
{
    [Fact]
    public void DiscoverEndpoints_ReturnsFilteredDescriptorsWithGeneratedToolNames()
    {
        var options = new DotnetMcpOptions { ExposureMode = McpExposureMode.OptIn };
        var provider = new TestApiDescriptionGroupCollectionProvider(
            ApiDescriptionTestHelper.Create(
                typeof(ExplicitController),
                nameof(ExplicitController.GetUser),
                "GET",
                "api/users/{id}"),
            ApiDescriptionTestHelper.Create(
                typeof(ExplicitController),
                nameof(ExplicitController.UpdateUser),
                "PUT",
                "api/users/{id}"));

        var service = new EndpointDiscoveryService(
            provider,
            new EndpointExposureFilter(options),
            new ToolNamingStrategy(options));

        var endpoints = service.DiscoverEndpoints();

        endpoints.Should().ContainSingle();
        endpoints[0].ToolName.Should().Be("get_users_by_id");
        endpoints[0].Description.Should().Be("Gets a user by id");
        endpoints[0].ReadOnly.Should().BeTrue();
        endpoints[0].Roles.Should().Contain("admin");
        endpoints[0].ControllerName.Should().Be("Explicit");
        endpoints[0].ActionName.Should().Be(nameof(ExplicitController.GetUser));
    }

    [Fact]
    public void DiscoverEndpoints_AppliesCustomToolNameFromAttribute()
    {
        var options = new DotnetMcpOptions { ExposureMode = McpExposureMode.OptIn };
        var provider = new TestApiDescriptionGroupCollectionProvider(
            ApiDescriptionTestHelper.Create(
                typeof(NamedToolController),
                nameof(NamedToolController.GetProfile),
                "GET",
                "api/profile"));

        var service = new EndpointDiscoveryService(
            provider,
            new EndpointExposureFilter(options),
            new ToolNamingStrategy(options));

        var endpoints = service.DiscoverEndpoints();

        endpoints.Should().ContainSingle();
        endpoints[0].ToolName.Should().Be("fetch_user_profile");
    }

    private sealed class TestApiDescriptionGroupCollectionProvider : IApiDescriptionGroupCollectionProvider
    {
        public TestApiDescriptionGroupCollectionProvider(params ApiDescription[] apiDescriptions)
        {
            ApiDescriptionGroups = new ApiDescriptionGroupCollection(
                new List<ApiDescriptionGroup>
                {
                    new("test", apiDescriptions)
                },
                1);
        }

        public ApiDescriptionGroupCollection ApiDescriptionGroups { get; }
    }
}
