using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using ModelContextProtocol.Client;

namespace DotnetMcp.AspNetCore.Tests;

public class McpOpenApiResourceIntegrationTests : IClassFixture<WebApplicationFactory<global::Program>>
{
    private readonly WebApplicationFactory<global::Program> _factory;

    public McpOpenApiResourceIntegrationTests(WebApplicationFactory<global::Program> factory)
    {
        _factory = factory.WithWebHostBuilder(_ => { });
    }

    [Fact]
    public async Task ListResources_Includes_OpenApi_Document()
    {
        await using var client = await CreateMcpClientAsync();

        var resources = await client.ListResourcesAsync();

        resources.Should().Contain(resource =>
            resource.Uri == "openapi://dotnet-mcp/document" &&
            resource.MimeType == "application/json");
    }

    [Fact]
    public async Task ReadResource_OpenApi_Returns_Document_Json()
    {
        await using var client = await CreateMcpClientAsync();

        var result = await client.ReadResourceAsync("openapi://dotnet-mcp/document");

        result.Contents.Should().NotBeNullOrEmpty();
        var text = result.Contents![0] as ModelContextProtocol.Protocol.TextResourceContents;
        text.Should().NotBeNull();
        text!.MimeType.Should().Be("application/json");
        text.Text.Should().Contain("\"openapi\"");
        text.Text.Should().Contain("/api/Users/{id}");
        text.Text.Should().Contain("get_users_by_id");
    }

    private async Task<IMcpClient> CreateMcpClientAsync()
    {
        var httpClient = _factory.CreateClient();
        var endpoint = new Uri(httpClient.BaseAddress!, "/mcp");

        var transport = new SseClientTransport(
            new SseClientTransportOptions
            {
                Endpoint = endpoint,
                UseStreamableHttp = true
            },
            httpClient);

        return await McpClientFactory.CreateAsync(transport);
    }
}
