using DotnetMcp.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace DotnetMcp;

public sealed class ToolNamingStrategy
{
    private readonly DotnetMcpOptions _options;

    public ToolNamingStrategy(DotnetMcpOptions options)
    {
        _options = options;
    }

    public string GenerateToolName(ApiDescription apiDescription, McpExposeAttribute? exposeAttribute = null)
    {
        if (!string.IsNullOrWhiteSpace(exposeAttribute?.Name))
        {
            return ApplyPrefix(exposeAttribute.Name.Trim());
        }

        var segments = new List<string>();

        if (_options.MapHttpMethods && !string.IsNullOrWhiteSpace(apiDescription.HttpMethod))
        {
            segments.Add(apiDescription.HttpMethod.ToLowerInvariant());
        }

        var path = apiDescription.RelativePath ?? string.Empty;
        foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (ShouldSkipPathSegment(part))
            {
                continue;
            }

            if (part.StartsWith('{') && part.EndsWith('}'))
            {
                var parameterName = part[1..^1];
                var constraintIndex = parameterName.IndexOf(':');
                if (constraintIndex >= 0)
                {
                    parameterName = parameterName[..constraintIndex];
                }

                segments.Add("by");
                segments.Add(SanitizeSegment(parameterName));
            }
            else
            {
                segments.Add(SanitizeSegment(part));
            }
        }

        if (segments.Count == 0)
        {
            segments.Add("root");
        }

        return ApplyPrefix(string.Join("_", segments));
    }

    private string ApplyPrefix(string toolName)
    {
        if (string.IsNullOrWhiteSpace(_options.ToolNamePrefix))
        {
            return toolName;
        }

        return $"{_options.ToolNamePrefix.Trim()}_{toolName}";
    }

    private static string SanitizeSegment(string segment)
    {
        return segment
            .Trim()
            .ToLowerInvariant()
            .Replace('-', '_');
    }

    private static bool ShouldSkipPathSegment(string segment)
    {
        return segment.Equals("api", StringComparison.OrdinalIgnoreCase);
    }
}
