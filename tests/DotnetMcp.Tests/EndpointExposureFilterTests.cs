using DotnetMcp.Abstractions;
using DotnetMcp.Tests.Helpers;
using FluentAssertions;

namespace DotnetMcp.Tests;

public class EndpointExposureFilterTests
{
    [Fact]
    public void OptIn_ExposesOnlyExplicitlyAnnotatedEndpoints()
    {
        var filter = CreateFilter(new DotnetMcpOptions { ExposureMode = McpExposureMode.OptIn });
        var exposed = ApiDescriptionTestHelper.Create(
            typeof(ExplicitController),
            nameof(ExplicitController.GetUser),
            "GET",
            "api/users/{id}");
        var hidden = ApiDescriptionTestHelper.Create(
            typeof(ExplicitController),
            nameof(ExplicitController.UpdateUser),
            "PUT",
            "api/users/{id}");

        filter.ShouldExpose(exposed).Should().BeTrue();
        filter.ShouldExpose(hidden).Should().BeFalse();
    }

    [Fact]
    public void OptIn_ExposesMethodsOnExposeAllControllerUnlessIgnored()
    {
        var filter = CreateFilter(new DotnetMcpOptions { ExposureMode = McpExposureMode.OptIn });
        var exposed = ApiDescriptionTestHelper.Create(
            typeof(ExposeAllController),
            nameof(ExposeAllController.GetItems),
            "GET",
            "api/items");
        var hidden = ApiDescriptionTestHelper.Create(
            typeof(ExposeAllController),
            nameof(ExposeAllController.GetSecret),
            "GET",
            "api/items/secret");

        filter.ShouldExpose(exposed).Should().BeTrue();
        filter.ShouldExpose(hidden).Should().BeFalse();
    }

    [Fact]
    public void OptOut_ExposesEndpointsUnlessIgnored()
    {
        var filter = CreateFilter(new DotnetMcpOptions { ExposureMode = McpExposureMode.OptOut });
        var exposed = ApiDescriptionTestHelper.Create(
            typeof(UnannotatedController),
            nameof(UnannotatedController.GetUsers),
            "GET",
            "api/users");
        var hidden = ApiDescriptionTestHelper.Create(
            typeof(IgnoredController),
            nameof(IgnoredController.GetBlocked),
            "GET",
            "api/blocked");

        filter.ShouldExpose(exposed).Should().BeTrue();
        filter.ShouldExpose(hidden).Should().BeFalse();
    }

    [Fact]
    public void OptOutWithRequireExplicitAnnotation_RequiresExplicitAnnotation()
    {
        var filter = CreateFilter(new DotnetMcpOptions
        {
            ExposureMode = McpExposureMode.OptOut,
            RequireExplicitAnnotation = true
        });
        var exposed = ApiDescriptionTestHelper.Create(
            typeof(ExplicitController),
            nameof(ExplicitController.GetUser),
            "GET",
            "api/users/{id}");
        var hidden = ApiDescriptionTestHelper.Create(
            typeof(UnannotatedController),
            nameof(UnannotatedController.GetUsers),
            "GET",
            "api/users");

        filter.ShouldExpose(exposed).Should().BeTrue();
        filter.ShouldExpose(hidden).Should().BeFalse();
    }

    [Fact]
    public void ExcludePaths_HidesMatchingRoutes()
    {
        var filter = CreateFilter(new DotnetMcpOptions
        {
            ExposureMode = McpExposureMode.OptOut,
            ExcludePaths = new[] { "api/internal" }
        });
        var hidden = ApiDescriptionTestHelper.Create(
            typeof(UnannotatedController),
            nameof(UnannotatedController.GetUsers),
            "GET",
            "api/internal/users");

        filter.ShouldExpose(hidden).Should().BeFalse();
    }

    [Fact]
    public void GlobalFilter_CanHideOtherwiseExposedEndpoints()
    {
        var filter = CreateFilter(new DotnetMcpOptions
        {
            ExposureMode = McpExposureMode.OptOut,
            Filter = descriptor => descriptor.HttpMethod != "DELETE"
        });
        var delete = ApiDescriptionTestHelper.Create(
            typeof(UnannotatedController),
            nameof(UnannotatedController.DeleteUser),
            "DELETE",
            "api/users/{id}");

        filter.ShouldExpose(delete).Should().BeFalse();
    }

    [Fact]
    public void McpIgnoreOnMethod_HidesEndpointInOptOutMode()
    {
        var filter = CreateFilter(new DotnetMcpOptions { ExposureMode = McpExposureMode.OptOut });
        var hidden = ApiDescriptionTestHelper.Create(
            typeof(ExposeAllController),
            nameof(ExposeAllController.GetSecret),
            "GET",
            "api/items/secret");

        filter.ShouldExpose(hidden).Should().BeFalse();
    }

    private static EndpointExposureFilter CreateFilter(DotnetMcpOptions options)
    {
        return new EndpointExposureFilter(options);
    }
}
