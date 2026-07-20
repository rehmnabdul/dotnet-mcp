using DotnetMcp.Abstractions;
using DotnetMcp.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Stdio MCP hosts reserve stdout for protocol messages — log to stderr only.
builder.UseDotnetMcpStdioLogging();

// Bind Kestrel to an ephemeral port; MCP traffic uses stdin/stdout.
builder.WebHost.UseUrls("http://127.0.0.1:0");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddDotnetMcp(options =>
{
    options.ExposureMode = McpExposureMode.OptIn;
    options.Transport = McpTransportMode.Stdio;
});

var app = builder.Build();

app.MapGet("/api/todos/{id}", (int id) => Results.Ok(new { id, title = "Learn MCP" }))
   .WithMcpExpose("get_todo", "Get todo by id", readOnly: true);

app.MapPost("/api/todos", (CreateTodoRequest request) =>
        Results.Created("/api/todos/1", request))
   .WithMcpExpose("create_todo", "Create a todo");

// MapDotnetMcp is a no-op for stdio; the hosted stdio transport starts with the app.
app.MapDotnetMcp();

app.Run();

public sealed record CreateTodoRequest(string Title);
