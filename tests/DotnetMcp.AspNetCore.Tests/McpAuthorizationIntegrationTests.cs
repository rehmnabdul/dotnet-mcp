using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace DotnetMcp.AspNetCore.Tests;

public class McpAuthorizationIntegrationTests : IClassFixture<WebApplicationFactory<global::Program>>
{
    private readonly WebApplicationFactory<global::Program> _factory;

    public McpAuthorizationIntegrationTests(WebApplicationFactory<global::Program> factory)
    {
        _factory = factory.WithWebHostBuilder(_ => { });
    }

    [Fact]
    public async Task CallTool_SecureEndpoint_WithoutAuth_ReturnsUnauthorized()
    {
        await using var client = await CreateMcpClientAsync();

        var response = await client.CallToolAsync(
            "get_secure_user",
            new Dictionary<string, object?>());

        response.IsError.Should().BeTrue();
        GetText(response).Should().Contain("401");
    }

    [Fact]
    public async Task CallTool_SecureEndpoint_WithAuth_Succeeds()
    {
        await using var client = await CreateMcpClientAsync(("X-Test-User", "ada"));

        var response = await client.CallToolAsync(
            "get_secure_user",
            new Dictionary<string, object?>());

        response.IsError.Should().BeFalse();
        GetText(response).Should().Contain("secure");
    }

    [Fact]
    public async Task CallTool_AdminEndpoint_WithWrongRole_ReturnsForbidden()
    {
        await using var client = await CreateMcpClientAsync(
            ("X-Test-User", "ada"),
            ("X-Test-Role", "User"));

        var response = await client.CallToolAsync(
            "get_admin_user",
            new Dictionary<string, object?>());

        response.IsError.Should().BeTrue();
        GetText(response).Should().Contain("403");
    }

    [Fact]
    public async Task CallTool_AdminEndpoint_WithAdminRole_Succeeds()
    {
        await using var client = await CreateMcpClientAsync(
            ("X-Test-User", "ada"),
            ("X-Test-Role", "Admin"));

        var response = await client.CallToolAsync(
            "get_admin_user",
            new Dictionary<string, object?>());

        response.IsError.Should().BeFalse();
        GetText(response).Should().Contain("admin");
    }

    [Fact]
    public async Task CallTool_McpExposeRoles_WithoutRole_ReturnsForbiddenOrUnauthorized()
    {
        await using var client = await CreateMcpClientAsync(("X-Test-User", "ada"));

        var response = await client.CallToolAsync(
            "get_mcp_role_user",
            new Dictionary<string, object?>());

        response.IsError.Should().BeTrue();
        GetText(response).Should().Match(text => text.Contains("401") || text.Contains("403"));
    }

    [Fact]
    public async Task CallTool_McpExposeRoles_WithAdminRole_Succeeds()
    {
        await using var client = await CreateMcpClientAsync(
            ("X-Test-User", "ada"),
            ("X-Test-Role", "Admin"));

        var response = await client.CallToolAsync(
            "get_mcp_role_user",
            new Dictionary<string, object?>());

        response.IsError.Should().BeFalse();
        GetText(response).Should().Contain("mcpRole");
    }

    private async Task<McpClient> CreateMcpClientAsync(params (string Name, string Value)[] headers)
    {
        var httpClient = _factory.CreateClient();
        var additionalHeaders = headers.ToDictionary(header => header.Name, header => header.Value);

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, "/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
                AdditionalHeaders = additionalHeaders
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
