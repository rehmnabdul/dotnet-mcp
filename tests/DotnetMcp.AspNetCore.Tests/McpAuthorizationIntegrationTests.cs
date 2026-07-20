using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using ModelContextProtocol.Client;

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
        response.Content.Should().NotBeNullOrEmpty();
        response.Content![0].Text.Should().Contain("401");
    }

    [Fact]
    public async Task CallTool_SecureEndpoint_WithAuth_Succeeds()
    {
        await using var client = await CreateMcpClientAsync(("X-Test-User", "ada"));

        var response = await client.CallToolAsync(
            "get_secure_user",
            new Dictionary<string, object?>());

        response.IsError.Should().BeFalse();
        response.Content.Should().NotBeNullOrEmpty();
        response.Content![0].Text.Should().Contain("secure");
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
        response.Content.Should().NotBeNullOrEmpty();
        response.Content![0].Text.Should().Contain("403");
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
        response.Content.Should().NotBeNullOrEmpty();
        response.Content![0].Text.Should().Contain("admin");
    }

    [Fact]
    public async Task CallTool_McpExposeRoles_WithoutRole_ReturnsForbiddenOrUnauthorized()
    {
        await using var client = await CreateMcpClientAsync(("X-Test-User", "ada"));

        var response = await client.CallToolAsync(
            "get_mcp_role_user",
            new Dictionary<string, object?>());

        response.IsError.Should().BeTrue();
        response.Content.Should().NotBeNullOrEmpty();
        response.Content![0].Text.Should().Match(text => text.Contains("401") || text.Contains("403"));
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
        response.Content.Should().NotBeNullOrEmpty();
        response.Content![0].Text.Should().Contain("mcpRole");
    }

    private async Task<IMcpClient> CreateMcpClientAsync(params (string Name, string Value)[] headers)
    {
        var httpClient = _factory.CreateClient();
        foreach (var (name, value) in headers)
        {
            httpClient.DefaultRequestHeaders.Add(name, value);
        }

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
