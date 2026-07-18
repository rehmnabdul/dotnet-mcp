using DotnetMcp.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

        services.AddSingleton<EndpointExposureFilter>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DotnetMcpOptions>>().Value;
            return new EndpointExposureFilter(options);
        });

        services.AddSingleton<ToolNamingStrategy>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DotnetMcpOptions>>().Value;
            return new ToolNamingStrategy(options);
        });

        services.AddSingleton<IEndpointDiscoveryService, EndpointDiscoveryService>();

        return services;
    }
}
