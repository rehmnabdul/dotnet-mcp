using FluentAssertions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace DotnetMcp.AspNetCore.Tests;

public class McpStdioIntegrationTests
{
    [Fact]
    public async Task StdioTransport_ListAndCallTools_Succeeds()
    {
        var dllPath = LocateTestAppDll();
        File.Exists(dllPath).Should().BeTrue($"expected TestApp at {dllPath}");

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "dotnet-mcp-testapp",
            Command = "dotnet",
            Arguments = ["exec", dllPath],
            EnvironmentVariables = new Dictionary<string, string?>
            {
                ["DOTNET_MCP_TRANSPORT"] = "stdio",
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["DOTNET_ROLL_FORWARD"] = "LatestMajor"
            }
        });

        await using var client = await McpClient.CreateAsync(transport);

        var tools = await client.ListToolsAsync();
        tools.Should().Contain(tool => tool.Name == "get_users_by_id");

        var response = await client.CallToolAsync(
            "get_users_by_id",
            new Dictionary<string, object?> { ["id"] = "7" });

        response.IsError.Should().BeFalse();
        var text = response.Content!.OfType<TextContentBlock>().FirstOrDefault();
        text.Should().NotBeNull();
        text!.Text.Should().Contain("7");
        text.Text.Should().Contain("Ada");
    }

    private static string LocateTestAppDll()
    {
        var testDir = Path.GetDirectoryName(typeof(McpStdioIntegrationTests).Assembly.Location)!;
        var configuration = testDir.Contains($"{Path.DirectorySeparatorChar}Debug{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            ? "Debug"
            : "Release";

        return Path.GetFullPath(Path.Combine(
            testDir,
            "..", "..", "..", "TestApp", "bin", configuration, "net8.0", "TestApp.dll"));
    }
}
