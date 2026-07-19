using DotnetMcp.Abstractions;
using DotnetMcp.AspNetCore;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMvcCore().AddApiExplorer();
builder.Services.AddControllers();
builder.Services.AddDotnetMcp(options =>
{
    options.ExposureMode = McpExposureMode.OptIn;
});

var app = builder.Build();

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
}

public sealed record CreateUserRequest(string Name);
