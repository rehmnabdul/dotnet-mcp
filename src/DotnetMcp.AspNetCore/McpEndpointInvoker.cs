using System.Text;
using System.Text.Json;
using DotnetMcp.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using ModelContextProtocol.Protocol;

namespace DotnetMcp.AspNetCore;

public sealed class McpEndpointInvoker
{
    private readonly IEnumerable<EndpointDataSource> _endpointDataSources;

    public McpEndpointInvoker(IEnumerable<EndpointDataSource> endpointDataSources)
    {
        _endpointDataSources = endpointDataSources;
    }

    public async Task<CallToolResponse> InvokeAsync(
        McpToolEntry tool,
        IReadOnlyDictionary<string, JsonElement>? arguments,
        HttpContext? sourceHttpContext,
        IServiceProvider requestServices,
        CancellationToken cancellationToken)
    {
        arguments ??= new Dictionary<string, JsonElement>();

        var context = new DefaultHttpContext
        {
            RequestServices = requestServices
        };

        context.Request.Method = tool.Descriptor.HttpMethod;
        context.Request.Scheme = sourceHttpContext?.Request.Scheme ?? "http";
        context.Request.Host = sourceHttpContext?.Request.Host ?? new HostString("localhost");
        context.Response.Body = new MemoryStream();

        if (sourceHttpContext?.Request.Headers.Authorization.Count > 0)
        {
            context.Request.Headers.Authorization = sourceHttpContext.Request.Headers.Authorization;
        }

        var path = BuildPath(tool.ApiDescription, arguments, out var routeValues);
        var query = BuildQueryString(tool.ApiDescription, arguments);
        context.Request.Path = path;
        context.Request.QueryString = query;
        context.Request.RouteValues = routeValues;

        var body = BuildRequestBody(tool.ApiDescription, arguments);
        if (body is not null)
        {
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            context.Request.Body = new MemoryStream(bodyBytes);
            context.Request.ContentLength = bodyBytes.Length;
            context.Request.ContentType = "application/json";
            context.Request.Body.Position = 0;
        }

        var endpoint = FindEndpoint(tool.Descriptor.HttpMethod, path);
        if (endpoint?.RequestDelegate is null)
        {
            return CreateErrorResponse($"No endpoint matched {tool.Descriptor.HttpMethod} {path}.");
        }

        context.SetEndpoint(endpoint);
        await endpoint.RequestDelegate(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        var responseBody = await reader.ReadToEndAsync(cancellationToken);

        var isError = context.Response.StatusCode >= 400;
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            responseBody = $"HTTP {context.Response.StatusCode}";
        }

        return new CallToolResponse
        {
            IsError = isError,
            Content =
            [
                new Content
                {
                    Type = "text",
                    Text = responseBody
                }
            ]
        };
    }

    private Endpoint? FindEndpoint(string httpMethod, PathString path)
    {
        var normalizedPath = NormalizePath(path.Value ?? string.Empty);

        foreach (var dataSource in _endpointDataSources)
        {
            foreach (var endpoint in dataSource.Endpoints)
            {
                if (endpoint is not RouteEndpoint routeEndpoint)
                {
                    continue;
                }

                var methods = routeEndpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods;
                if (methods is not null &&
                    !methods.Any(method => string.Equals(method, httpMethod, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var routePattern = routeEndpoint.RoutePattern.RawText;
                if (RouteMatches(normalizedPath, routePattern))
                {
                    return endpoint;
                }
            }
        }

        return null;
    }

    private static bool RouteMatches(string requestPath, string? routePattern)
    {
        if (string.IsNullOrWhiteSpace(routePattern))
        {
            return false;
        }

        var normalizedPattern = NormalizePath(routePattern);
        var patternSegments = normalizedPattern.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var actualSegments = requestPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (patternSegments.Length != actualSegments.Length)
        {
            return false;
        }

        for (var i = 0; i < patternSegments.Length; i++)
        {
            var patternSegment = patternSegments[i];
            if (patternSegment.StartsWith('{') && patternSegment.EndsWith('}'))
            {
                continue;
            }

            if (!string.Equals(patternSegment, actualSegments[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizePath(string path)
    {
        return path.Trim().TrimStart('/');
    }

    private static CallToolResponse CreateErrorResponse(string message)
    {
        return new CallToolResponse
        {
            IsError = true,
            Content =
            [
                new Content
                {
                    Type = "text",
                    Text = message
                }
            ]
        };
    }

    private static PathString BuildPath(
        ApiDescription apiDescription,
        IReadOnlyDictionary<string, JsonElement> arguments,
        out RouteValueDictionary routeValues)
    {
        routeValues = new RouteValueDictionary();
        var path = apiDescription.RelativePath ?? "/";
        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        foreach (var parameter in apiDescription.ParameterDescriptions)
        {
            if (parameter.Source != BindingSource.Path || !arguments.TryGetValue(parameter.Name, out var value))
            {
                continue;
            }

            var token = value.ToString();
            routeValues[parameter.Name] = token;
            path = path.Replace($"{{{parameter.Name}}}", token, StringComparison.OrdinalIgnoreCase);
            path = path.Replace($"{{{parameter.Name}:int}}", token, StringComparison.OrdinalIgnoreCase);
            path = path.Replace($"{{{parameter.Name}:guid}}", token, StringComparison.OrdinalIgnoreCase);
        }

        return path;
    }

    private static QueryString BuildQueryString(ApiDescription apiDescription, IReadOnlyDictionary<string, JsonElement> arguments)
    {
        var builder = new QueryBuilder();

        foreach (var parameter in apiDescription.ParameterDescriptions)
        {
            if (parameter.Source != BindingSource.Query || !arguments.TryGetValue(parameter.Name, out var value))
            {
                continue;
            }

            builder.Add(parameter.Name, value.ToString());
        }

        return builder.ToQueryString();
    }

    private static string? BuildRequestBody(ApiDescription apiDescription, IReadOnlyDictionary<string, JsonElement> arguments)
    {
        var bodyParameters = apiDescription.ParameterDescriptions
            .Where(parameter => parameter.Source == BindingSource.Body)
            .ToList();

        if (bodyParameters.Count == 1 && arguments.TryGetValue(bodyParameters[0].Name, out var bodyValue))
        {
            return bodyValue.GetRawText();
        }

        var bodyObject = new Dictionary<string, JsonElement>();
        foreach (var parameter in apiDescription.ParameterDescriptions)
        {
            if (parameter.Source == BindingSource.Path ||
                parameter.Source == BindingSource.Query ||
                parameter.Source == BindingSource.Services)
            {
                continue;
            }

            if (arguments.TryGetValue(parameter.Name, out var value))
            {
                bodyObject[parameter.Name] = value;
            }
        }

        return bodyObject.Count == 0 ? null : JsonSerializer.Serialize(bodyObject);
    }
}
