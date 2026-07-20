using System.Security.Claims;
using System.Text.Encodings.Web;
using DotnetMcp.Abstractions;
using DotnetMcp.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMvcCore().AddApiExplorer();
builder.Services.AddControllers();
builder.Services.AddAuthentication("Test")
    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
builder.Services.AddAuthorization();
builder.Services.AddDotnetMcp(options =>
{
    options.ExposureMode = McpExposureMode.OptIn;
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapDotnetMcp("/mcp");

app.Run();

public partial class Program { }

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    [HttpGet("{id}")]
    [McpExpose(Description = "Get user by id", ReadOnly = true)]
    public IActionResult Get(string id) => Ok(new { id, name = "Ada" });

    [HttpPost]
    [McpExpose(Description = "Create user")]
    public IActionResult Create([FromBody] CreateUserRequest request) =>
        Created(string.Empty, request);

    [HttpGet("secure")]
    [Authorize]
    [McpExpose(Name = "get_secure_user", Description = "Requires authentication")]
    public IActionResult GetSecure() => Ok(new { secure = true });

    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    [McpExpose(Name = "get_admin_user", Description = "Requires Admin role")]
    public IActionResult GetAdmin() => Ok(new { admin = true });

    [HttpGet("mcp-role")]
    [AllowAnonymous]
    [McpExpose(Name = "get_mcp_role_user", Description = "Requires MCP Admin role", Roles = ["Admin"])]
    public IActionResult GetMcpRole() => Ok(new { mcpRole = true });
}

public sealed record CreateUserRequest(string Name);

public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-User", out var userValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userName = userValues.ToString();
        if (string.IsNullOrWhiteSpace(userName))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, userName),
            new(ClaimTypes.NameIdentifier, userName)
        };

        if (Request.Headers.TryGetValue("X-Test-Role", out var roleValues))
        {
            var role = roleValues.ToString();
            if (!string.IsNullOrWhiteSpace(role))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
