using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DotnetMcp.Abstractions;
using DotnetMcp.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Dev-only signing key for the sample. Replace in real apps.
const string signingKey = "dotnet-mcp-sample-dev-key-32chars!!";
const string issuer = "dotnet-mcp-sample";
const string audience = "dotnet-mcp-clients";

builder.Services.AddEndpointsApiExplorer();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TodosRead", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("TodosAdmin", policy => policy.RequireRole("Admin"));
});

builder.Services.AddDotnetMcp(options =>
{
    options.ExposureMode = McpExposureMode.OptIn;
    options.OpenApiTitle = "Todo API (Auth Sample)";
    options.OpenApiVersion = "1.0.0";
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Mint a demo JWT (HTTP only — not exposed as an MCP tool).
app.MapPost("/auth/token", (TokenRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Username))
    {
        return Results.BadRequest(new { error = "username is required" });
    }

    var role = string.IsNullOrWhiteSpace(request.Role) ? "User" : request.Role.Trim();
    var claims = new List<Claim>
    {
        new(ClaimTypes.Name, request.Username),
        new(ClaimTypes.NameIdentifier, request.Username),
        new(ClaimTypes.Role, role)
    };

    var credentials = new SigningCredentials(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
        SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: issuer,
        audience: audience,
        claims: claims,
        expires: DateTime.UtcNow.AddHours(1),
        signingCredentials: credentials);

    var jwt = new JwtSecurityTokenHandler().WriteToken(token);
    return Results.Ok(new { access_token = jwt, token_type = "Bearer", role });
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/todos/{id}", (int id) => Results.Ok(new { id, title = "Secure todo" }))
   .RequireAuthorization("TodosRead")
   .WithMcpExpose("get_todo", "Get a todo (requires authenticated user)", readOnly: true);

app.MapPost("/api/todos", (CreateTodoRequest request) => Results.Created("/api/todos/1", request))
   .RequireAuthorization("TodosAdmin")
   .WithMcpExpose("create_todo", "Create a todo (requires Admin role)");

app.MapDelete("/api/todos/{id}", (int id) => Results.NoContent())
   .RequireAuthorization("TodosAdmin")
   .WithMcpExpose("delete_todo", "Delete a todo (requires Admin role)", destructive: true);

// HTTP allows anonymous access, but MCP tool calls still require the Analyst role.
app.MapGet("/api/analytics/summary", () => Results.Ok(new { todos = 42, completionRate = 0.81 }))
   .AllowAnonymous()
   .WithMcpExpose(
       "get_analytics_summary",
       "Analytics summary (MCP requires Analyst role)",
       readOnly: true,
       roles: ["Analyst"]);

app.MapDotnetMcp("/mcp");

app.Run();

public sealed record TokenRequest(string Username, string? Role);
public sealed record CreateTodoRequest(string Title);
