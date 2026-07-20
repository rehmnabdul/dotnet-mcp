using DotnetMcp.Abstractions;
using FluentAssertions;

namespace DotnetMcp.Tests;

public class DotnetMcpOptionsTests
{
    [Fact]
    public void Defaults_Use_Secure_OptIn_Exposure()
    {
        var options = new DotnetMcpOptions();

        options.ExposureMode.Should().Be(McpExposureMode.OptIn);
        options.EnforceEndpointAuthorization.Should().BeTrue();
        options.EnableOpenApiResource.Should().BeTrue();
        options.Transport.Should().Be(McpTransportMode.Http);
    }
}
