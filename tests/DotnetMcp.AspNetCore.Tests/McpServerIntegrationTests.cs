using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace DotnetMcp.AspNetCore.Tests;

public class McpServerIntegrationTests : IClassFixture<WebApplicationFactory<global::Program>>
{
    private readonly WebApplicationFactory<global::Program> _factory;

    public McpServerIntegrationTests(WebApplicationFactory<global::Program> factory)
    {
        _factory = factory.WithWebHostBuilder(_ => { });
    }

    [Fact]
    public async Task ListTools_Includes_Annotated_Endpoints()
    {
        await using var client = await CreateMcpClientAsync();

        var tools = await client.ListToolsAsync();

        tools.Should().Contain(tool => tool.Name == "get_users_by_id");
        tools.Should().Contain(tool => tool.Name == "post_users");
    }

    [Fact]
    public async Task CallTool_GetUser_Returns_Expected_Json()
    {
        await using var client = await CreateMcpClientAsync();

        var response = await client.CallToolAsync(
            "get_users_by_id",
            new Dictionary<string, object?> { ["id"] = "42" });

        response.IsError.Should().BeFalse();
        GetText(response).Should().Contain("42");
        GetText(response).Should().Contain("Ada");
    }

    [Fact]
    public async Task CallTool_CreateUser_Returns_Created_Response()
    {
        await using var client = await CreateMcpClientAsync();

        var response = await client.CallToolAsync(
            "post_users",
            new Dictionary<string, object?> { ["request"] = new { name = "Grace" } });

        response.IsError.Should().BeFalse();
        GetText(response).Should().Contain("Grace");
    }

    private async Task<McpClient> CreateMcpClientAsync()
    {
        var httpClient = _factory.CreateClient();
        var endpoint = new Uri(httpClient.BaseAddress!, "/mcp");

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = endpoint,
                TransportMode = HttpTransportMode.StreamableHttp
            },
            httpClient);

        return await McpClient.CreateAsync(transport);
    }

    private static string GetText(CallToolResult response)
    {
        response.Content.Should().NotBeNullOrEmpty();
        var block = response.Content!.OfType<TextContentBlock>().FirstOrDefault();
        block.Should().NotBeNull();
        return block!.Text;
    }
}
