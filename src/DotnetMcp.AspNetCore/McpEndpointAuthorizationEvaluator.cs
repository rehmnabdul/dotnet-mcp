using System.Security.Claims;
using DotnetMcp.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DotnetMcp.AspNetCore;

public sealed class McpEndpointAuthorizationEvaluator
{
    private readonly DotnetMcpOptions _options;

    public McpEndpointAuthorizationEvaluator(IOptions<DotnetMcpOptions> options)
    {
        _options = options.Value;
    }

    public async Task<McpAuthorizationResult> AuthorizeAsync(
        Endpoint endpoint,
        EndpointDescriptor descriptor,
        HttpContext? sourceHttpContext,
        IServiceProvider requestServices,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.EnforceEndpointAuthorization)
        {
            return McpAuthorizationResult.Success();
        }

        var allowAnonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
        var authorizeData = CollectAuthorizeData(endpoint, descriptor, allowAnonymous);

        if (authorizeData.Count == 0)
        {
            return McpAuthorizationResult.Success();
        }

        var policyProvider = requestServices.GetService<IAuthorizationPolicyProvider>();
        var policyEvaluator = requestServices.GetService<IPolicyEvaluator>();
        if (policyProvider is null || policyEvaluator is null)
        {
            return McpAuthorizationResult.Failure(
                StatusCodes.Status401Unauthorized,
                "Authorization services are not configured for this application.");
        }

        var policy = await AuthorizationPolicy.CombineAsync(policyProvider, authorizeData);
        if (policy is null)
        {
            return McpAuthorizationResult.Success();
        }

        var user = sourceHttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
        var evaluationContext = sourceHttpContext ?? new DefaultHttpContext
        {
            User = user,
            RequestServices = requestServices
        };

        if (sourceHttpContext is null)
        {
            evaluationContext.User = user;
        }

        var authenticateResult = await policyEvaluator.AuthenticateAsync(policy, evaluationContext);
        var authorizeResult = await policyEvaluator.AuthorizeAsync(
            policy,
            authenticateResult,
            evaluationContext,
            resource: null);

        if (authorizeResult.Succeeded)
        {
            return McpAuthorizationResult.Success();
        }

        var isAuthenticated = authenticateResult.Succeeded
            && evaluationContext.User.Identity?.IsAuthenticated == true;

        return isAuthenticated
            ? McpAuthorizationResult.Failure(StatusCodes.Status403Forbidden, "Forbidden.")
            : McpAuthorizationResult.Failure(StatusCodes.Status401Unauthorized, "Authentication required.");
    }

    private static List<IAuthorizeData> CollectAuthorizeData(
        Endpoint endpoint,
        EndpointDescriptor descriptor,
        bool allowAnonymous)
    {
        var authorizeData = new List<IAuthorizeData>();

        // AllowAnonymous skips endpoint [Authorize] metadata, but MCP Roles still apply.
        if (!allowAnonymous)
        {
            authorizeData.AddRange(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>());
        }

        if (descriptor.Roles.Count > 0)
        {
            authorizeData.Add(new AuthorizeAttribute
            {
                Roles = string.Join(",", descriptor.Roles)
            });
        }

        return authorizeData;
    }
}

public readonly struct McpAuthorizationResult
{
    private McpAuthorizationResult(bool succeeded, int statusCode, string? message)
    {
        Succeeded = succeeded;
        StatusCode = statusCode;
        Message = message;
    }

    public bool Succeeded { get; }

    public int StatusCode { get; }

    public string? Message { get; }

    public static McpAuthorizationResult Success() => new(true, StatusCodes.Status200OK, null);

    public static McpAuthorizationResult Failure(int statusCode, string message) =>
        new(false, statusCode, message);
}
