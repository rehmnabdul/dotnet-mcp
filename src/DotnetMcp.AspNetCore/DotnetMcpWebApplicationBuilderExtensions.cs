using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

namespace DotnetMcp.AspNetCore;

public static class DotnetMcpWebApplicationBuilderExtensions
{
    /// <summary>
    /// Configures console logging to stderr. Required for stdio MCP servers so protocol
    /// messages on stdout are not corrupted by log output.
    /// </summary>
    public static WebApplicationBuilder UseDotnetMcpStdioLogging(this WebApplicationBuilder builder)
    {
        builder.Logging.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        return builder;
    }
}