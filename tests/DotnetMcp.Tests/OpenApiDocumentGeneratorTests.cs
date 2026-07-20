using DotnetMcp.Abstractions;
using DotnetMcp.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Options;

namespace DotnetMcp.Tests;

public class OpenApiDocumentGeneratorTests
{
    [Fact]
    public void Generate_IncludesExposedPathsAndOperationIds()
    {
        var options = new DotnetMcpOptions
        {
            ExposureMode = McpExposureMode.OptIn,
            OpenApiTitle = "Test API",
            OpenApiVersion = "9.9.9"
        };

        var provider = new TestApiDescriptionGroupCollectionProvider(
            ApiDescriptionTestHelper.Create(
                typeof(ExplicitController),
                nameof(ExplicitController.GetUser),
                "GET",
                "api/users/{id:int}"),
            ApiDescriptionTestHelper.Create(
                typeof(ExplicitController),
                nameof(ExplicitController.UpdateUser),
                "PUT",
                "api/users/{id}"));

        var catalog = new McpToolCatalog(
            provider,
            new EndpointExposureFilter(options),
            new ToolNamingStrategy(options),
            new ToolSchemaGenerator());

        var generator = new OpenApiDocumentGenerator(catalog, Options.Create(options));

        var document = generator.Generate();
        var json = generator.GenerateJson();

        document["openapi"]!.GetValue<string>().Should().Be("3.0.1");
        document["info"]!["title"]!.GetValue<string>().Should().Be("Test API");
        document["info"]!["version"]!.GetValue<string>().Should().Be("9.9.9");

        var paths = document["paths"]!.AsObject();
        paths.Should().ContainKey("/api/users/{id}");
        paths["/api/users/{id}"]!["get"]!["operationId"]!.GetValue<string>()
            .Should().Be("get_users_by_id");

        json.Should().Contain("openapi");
        json.Should().Contain("/api/users/{id}");
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
