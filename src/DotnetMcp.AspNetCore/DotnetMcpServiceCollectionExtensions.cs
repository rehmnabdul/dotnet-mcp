using DotnetMcp.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DotnetMcp.AspNetCore;

public static class DotnetMcpServiceCollectionExtensions
{
    public static IServiceCollection AddDotnetMcp(
        this IServiceCollection services,
        Action<DotnetMcpOptions>? configure = null)
    {
        services.AddOptions<DotnetMcpOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<EndpointExposureFilter>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DotnetMcpOptions>>().Value;
            return new EndpointExposureFilter(options);
        });

        services.TryAddSingleton<ToolNamingStrategy>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DotnetMcpOptions>>().Value;
            return new ToolNamingStrategy(options);
        });

        services.TryAddSingleton<ToolSchemaGenerator>();
        services.TryAddSingleton<McpToolCatalog>();
        services.TryAddSingleton<IEndpointDiscoveryService, EndpointDiscoveryService>();
        services.TryAddSingleton<McpEndpointAuthorizationEvaluator>();
        services.TryAddSingleton<McpEndpointInvoker>();
        services.TryAddSingleton<McpToolHandlers>();

        services.AddHttpContextAccessor();

        services.AddMcpServer(options =>
        {
            options.ServerInfo = new Implementation
            {
                Name = "dotnet-mcp",
                Version = typeof(DotnetMcpServiceCollectionExtensions).Assembly.GetName().Version?.ToString() ?? "0.1.0"
            };
        })
        // Stateless mode keeps IHttpContextAccessor/User available during tools/call
        // (required for auth enforcement on ModelContextProtocol.AspNetCore 0.2.x).
        .WithHttpTransport(options => options.Stateless = true)
        .WithListToolsHandler((context, cancellationToken) =>
        {
            var handlers = context.Services!.GetRequiredService<McpToolHandlers>();
            return handlers.ListToolsAsync(context, cancellationToken);
        })
        .WithCallToolHandler((context, cancellationToken) =>
        {
            var handlers = context.Services!.GetRequiredService<McpToolHandlers>();
            return handlers.CallToolAsync(context, cancellationToken);
        });

        return services;
    }
}
