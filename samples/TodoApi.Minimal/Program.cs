using DotnetMcp.Abstractions;
using DotnetMcp.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddDotnetMcp(options =>
{
    options.ExposureMode = McpExposureMode.OptIn;
});

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/todos/{id}", (int id) => Results.Ok(new { id, title = "Learn MCP" }))
   .WithMcpExpose("get_todo", "Get todo by id", readOnly: true);

app.MapPost("/api/todos", (CreateTodoRequest request) => Results.Created("/api/todos/1", request))
   .WithMcpExpose("create_todo", "Create a todo");

app.MapDotnetMcp("/mcp");

app.Run();

public sealed record CreateTodoRequest(string Title);
