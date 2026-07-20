using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DotnetMcp.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace DotnetMcp;

/// <summary>
/// Builds an OpenAPI 3.0 document from MCP-exposed endpoints.
/// </summary>
public sealed class OpenApiDocumentGenerator
{
    private static readonly Regex RouteConstraintRegex = new(
        @"\{([^}:]+)(:[^}]+)?\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly McpToolCatalog _toolCatalog;
    private readonly DotnetMcpOptions _options;

    public OpenApiDocumentGenerator(
        McpToolCatalog toolCatalog,
        IOptions<DotnetMcpOptions> options)
    {
        _toolCatalog = toolCatalog;
        _options = options.Value;
    }

    public string GenerateJson()
    {
        return Generate().ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    public JsonObject Generate()
    {
        var paths = new JsonObject();

        foreach (var tool in _toolCatalog.GetTools())
        {
            var pathKey = NormalizePath(tool.ApiDescription.RelativePath);
            if (!paths.TryGetPropertyValue(pathKey, out var existingPathNode) || existingPathNode is not JsonObject pathItem)
            {
                pathItem = new JsonObject();
                paths[pathKey] = pathItem;
            }

            var method = (tool.Descriptor.HttpMethod ?? "get").ToLowerInvariant();
            pathItem[method] = CreateOperation(tool);
        }

        return new JsonObject
        {
            ["openapi"] = "3.0.1",
            ["info"] = new JsonObject
            {
                ["title"] = string.IsNullOrWhiteSpace(_options.OpenApiTitle) ? "dotnet-mcp" : _options.OpenApiTitle,
                ["version"] = string.IsNullOrWhiteSpace(_options.OpenApiVersion) ? "1.0.0" : _options.OpenApiVersion,
                ["description"] = "OpenAPI document generated from MCP-exposed ASP.NET Core endpoints."
            },
            ["paths"] = paths
        };
    }

    private static JsonObject CreateOperation(McpToolEntry tool)
    {
        var operation = new JsonObject
        {
            ["operationId"] = tool.Descriptor.ToolName,
            ["summary"] = tool.Descriptor.Description ?? tool.Descriptor.ToolName,
            ["responses"] = new JsonObject
            {
                ["200"] = new JsonObject
                {
                    ["description"] = "Success"
                }
            }
        };

        var parameters = new JsonArray();

        foreach (var parameter in tool.ApiDescription.ParameterDescriptions)
        {
            if (parameter.Source == BindingSource.Services)
            {
                continue;
            }

            if (parameter.Source == BindingSource.Body)
            {
                operation["requestBody"] = new JsonObject
                {
                    ["required"] = parameter.IsRequired,
                    ["content"] = new JsonObject
                    {
                        ["application/json"] = new JsonObject
                        {
                            ["schema"] = BuildSchema(parameter.Type)
                        }
                    }
                };
                continue;
            }

            var location = parameter.Source == BindingSource.Path ? "path"
                : parameter.Source == BindingSource.Header ? "header"
                : "query";

            parameters.Add(new JsonObject
            {
                ["name"] = parameter.Name,
                ["in"] = location,
                ["required"] = parameter.Source == BindingSource.Path || parameter.IsRequired,
                ["schema"] = BuildSchema(parameter.Type)
            });
        }

        if (parameters.Count > 0)
        {
            operation["parameters"] = parameters;
        }

        return operation;
    }

    private static JsonObject BuildSchema(Type? type)
    {
        if (type is null)
        {
            return new JsonObject { ["type"] = "string" };
        }

        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying == typeof(bool))
        {
            return new JsonObject { ["type"] = "boolean" };
        }

        if (underlying == typeof(int) || underlying == typeof(long) || underlying == typeof(short) || underlying == typeof(byte))
        {
            return new JsonObject { ["type"] = "integer" };
        }

        if (underlying == typeof(float) || underlying == typeof(double) || underlying == typeof(decimal))
        {
            return new JsonObject { ["type"] = "number" };
        }

        if (underlying.IsEnum)
        {
            var values = new JsonArray();
            foreach (var name in Enum.GetNames(underlying))
            {
                values.Add(name);
            }

            return new JsonObject { ["type"] = "string", ["enum"] = values };
        }

        if (underlying.IsClass && underlying != typeof(string))
        {
            var properties = new JsonObject();
            foreach (var property in underlying.GetProperties().Where(property => property.CanRead))
            {
                properties[ToCamelCase(property.Name)] = BuildSchema(property.PropertyType);
            }

            return new JsonObject
            {
                ["type"] = "object",
                ["properties"] = properties
            };
        }

        return new JsonObject { ["type"] = "string" };
    }

    private static string NormalizePath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return "/";
        }

        var path = relativePath.Trim();
        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        return RouteConstraintRegex.Replace(path, "{$1}");
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
        {
            return name;
        }

        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
