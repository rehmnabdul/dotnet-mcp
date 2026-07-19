# dotnet-mcp

A .NET library that exposes your ASP.NET Core APIs as an [MCP (Model Context Protocol)](https://modelcontextprotocol.io/) server. AI assistants and agents can discover your endpoints as MCP tools, inspect JSON Schema for parameters, and invoke them over HTTP with minimal configuration.

**Current release:** [`0.1.0-alpha`](https://www.nuget.org/packages/DotnetMcp.AspNetCore/0.1.0-alpha) (pre-release)

## Features

- **Automatic tool discovery** — uses ASP.NET Core API Explorer to discover Minimal API and controller endpoints
- **Opt-in or opt-out exposure** — expose only annotated endpoints, or expose everything except ignored ones
- **JSON Schema generation** — tool input schemas are built from route, query, and body parameters
- **In-process invocation** — MCP tool calls dispatch through the same endpoint pipeline as normal HTTP requests
- **HTTP transport** — MCP over streamable HTTP/SSE via the official `ModelContextProtocol.AspNetCore` SDK
- **Auth forwarding** — `Authorization` headers from the MCP request are forwarded to invoked endpoints

## Packages

| Package | NuGet | Description |
|---------|-------|-------------|
| `DotnetMcp.AspNetCore` | [nuget.org](https://www.nuget.org/packages/DotnetMcp.AspNetCore) | **Install this** — ASP.NET Core integration and MCP endpoint |
| `DotnetMcp.Core` | [nuget.org](https://www.nuget.org/packages/DotnetMcp.Core) | Discovery, filtering, schema generation, tool naming |
| `DotnetMcp.Abstractions` | [nuget.org](https://www.nuget.org/packages/DotnetMcp.Abstractions) | Attributes, options, and contracts |

> The middle package is published as `DotnetMcp.Core` because the `DotnetMcp` package ID is already taken on nuget.org.

## Requirements

- .NET 8.0+
- ASP.NET Core with API Explorer enabled (`AddEndpointsApiExplorer()` for Minimal APIs, or `AddControllers()` / `AddMvcCore().AddApiExplorer()` for controllers)

## Quick start

### 1. Install

```bash
dotnet add package DotnetMcp.AspNetCore --version 0.1.0-alpha
```

### 2. Register services and map the MCP endpoint

```csharp
using DotnetMcp.Abstractions;
using DotnetMcp.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddDotnetMcp(options =>
{
    options.ExposureMode = McpExposureMode.OptIn;
});

var app = builder.Build();

app.MapGet("/api/todos/{id}", (int id) => Results.Ok(new { id, title = "Learn MCP" }))
   .WithMcpExpose("get_todo", "Get todo by id", readOnly: true);

app.MapPost("/api/todos", (CreateTodoRequest request) =>
        Results.Created("/api/todos/1", request))
   .WithMcpExpose("create_todo", "Create a todo");

app.MapDotnetMcp("/mcp");

app.Run();

public sealed record CreateTodoRequest(string Title);
```

### 3. Connect an MCP client

The MCP server is available at `/mcp` (default). Use any MCP client that supports HTTP/SSE transport, for example the official .NET client:

```csharp
using ModelContextProtocol.Client;

var httpClient = new HttpClient { BaseAddress = new Uri("https://localhost:5001") };
var transport = new SseClientTransport(
    new SseClientTransportOptions
    {
        Endpoint = new Uri(httpClient.BaseAddress!, "/mcp"),
        UseStreamableHttp = true
    },
    httpClient);

await using var client = await McpClientFactory.CreateAsync(transport);

var tools = await client.ListToolsAsync();
var result = await client.CallToolAsync("get_todo", new Dictionary<string, object?> { ["id"] = 1 });
```

Run the included sample to try it locally:

```bash
dotnet run --project samples/TodoApi.Minimal
```

## How it works

```
┌─────────────┐     tools/list, tools/call      ┌──────────────────┐
│  MCP Client │ ◄──────────────────────────────►│  /mcp endpoint   │
└─────────────┘         HTTP / SSE              └────────┬─────────┘
                                                         │
                                              ┌──────────▼──────────┐
                                              │  McpToolCatalog     │
                                              │  (discovered tools) │
                                              └──────────┬──────────┘
                                                         │
                                              ┌──────────▼──────────┐
                                              │  McpEndpointInvoker │
                                              │  (in-process HTTP)  │
                                              └──────────┬──────────┘
                                                         │
                                              ┌──────────▼──────────┐
                                              │  Your API endpoints │
                                              └─────────────────────┘
```

1. **`AddDotnetMcp()`** registers discovery, schema generation, and MCP server handlers.
2. **`MapDotnetMcp("/mcp")`** maps the MCP HTTP endpoint using the official MCP ASP.NET Core SDK.
3. On **`tools/list`**, exposed endpoints are returned as MCP tools with JSON Schema input definitions.
4. On **`tools/call`**, arguments are mapped to route, query, and body parameters and the matching endpoint is invoked in-process.

## Configuration

Configure via the `AddDotnetMcp` callback:

```csharp
builder.Services.AddDotnetMcp(options =>
{
    options.ExposureMode = McpExposureMode.OptIn;
    options.MapHttpMethods = true;
    options.ToolNamePrefix = "myapp";
    options.ExcludePaths = new[] { "internal/", "health" };
    options.McpRoutePattern = "/mcp";
    options.Filter = descriptor => !descriptor.Route.Contains("admin");
});
```

| Option | Default | Description |
|--------|---------|-------------|
| `ExposureMode` | `OptOut` | `OptIn` exposes only annotated endpoints; `OptOut` exposes all except ignored |
| `RequireExplicitAnnotation` | `false` | When `OptOut`, require `[McpExpose]` / `[McpExposeAll]` / `.WithMcpExpose()` |
| `MapHttpMethods` | `true` | Prefix auto-generated tool names with the HTTP method (e.g. `get_users_by_id`) |
| `ToolNamePrefix` | `""` | Prefix applied to all tool names |
| `ExcludePaths` | `[]` | Path prefixes to never expose (case-insensitive) |
| `McpRoutePattern` | `"/mcp"` | Documented default route pattern (map with `MapDotnetMcp`) |
| `Filter` | `null` | Additional predicate over `EndpointDescriptor` |

## Exposing endpoints

### Exposure modes

**Opt-in (recommended for production)**

Only endpoints you explicitly mark are exposed as MCP tools.

```csharp
options.ExposureMode = McpExposureMode.OptIn;
```

**Opt-out**

All discovered endpoints are exposed unless ignored or excluded.

```csharp
options.ExposureMode = McpExposureMode.OptOut;
options.ExcludePaths = new[] { "health", "internal/" };
```

### Minimal APIs — `.WithMcpExpose()`

```csharp
app.MapDelete("/api/todos/{id}", (int id) => Results.NoContent())
   .WithMcpExpose("delete_todo", "Delete a todo", destructive: true);
```

Parameters:

| Parameter | Description |
|-----------|-------------|
| `name` | Explicit MCP tool name (recommended) |
| `description` | Shown to MCP clients in `tools/list` |
| `readOnly` | Sets the MCP read-only hint (defaults from HTTP method for GET/HEAD) |
| `destructive` | Sets the MCP destructive hint (defaults to `true` for DELETE) |

### Controllers — `[McpExpose]`

```csharp
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
```

### Hide endpoints — `[McpIgnore]`

```csharp
[HttpGet("health")]
[McpIgnore]
public IActionResult Health() => Ok();
```

Apply to a controller class to ignore all its actions.

### Expose all actions on a controller — `[McpExposeAll]`

```csharp
[McpExposeAll]
[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    // All actions are exposed unless individually ignored
}
```

## Tool naming

When no explicit name is provided, names are generated from the HTTP method and route:

| Route | HTTP | Generated tool name |
|-------|------|---------------------|
| `/api/users/{id}` | GET | `get_users_by_id` |
| `/api/users` | POST | `post_users` |
| `/api/todos/{id}` | DELETE | `delete_todos_by_id` |

Rules:

- HTTP method is included when `MapHttpMethods` is `true`
- Path segments are lowercased; `-` becomes `_`
- The `api` segment is skipped
- Route parameters become `by_{param}` (e.g. `{id}` → `by_id`)
- `ToolNamePrefix` is prepended when set

## Tool input schema

Parameters from API Explorer become JSON Schema properties:

- **Route** parameters → required/optional based on model metadata
- **Query** parameters → query string on invocation
- **Body** parameters → JSON request body (single body param or composite object)
- Supported types include `string`, `bool`, integer types, `decimal`/`double`/`float`, enums, arrays, and nested objects

Example schema for `GET /api/users/{id}`:

```json
{
  "type": "object",
  "properties": {
    "id": { "type": "string" }
  },
  "required": ["id"]
}
```

## MCP tool invocation

When a client calls a tool:

1. Route parameters are substituted into the path
2. Query parameters are appended to the query string
3. Remaining arguments are serialized as JSON body for POST/PUT/PATCH
4. The matching endpoint is found and invoked in-process
5. The HTTP response body is returned as MCP text content
6. HTTP status codes ≥ 400 are reported as tool errors

The `Authorization` header on the MCP HTTP request is forwarded to the invoked endpoint, so your existing auth middleware continues to apply.

## Project structure

```
dotnet-mcp/
├── src/
│   ├── DotnetMcp.Abstractions/   # Attributes, options, contracts
│   ├── DotnetMcp/                # Discovery, filtering, schemas (NuGet: DotnetMcp.Core)
│   └── DotnetMcp.AspNetCore/     # MCP server wiring and invocation
├── samples/
│   └── TodoApi.Minimal/          # Reference Minimal API app
├── tests/
│   ├── DotnetMcp.Tests/          # Unit tests
│   └── DotnetMcp.AspNetCore.Tests/  # Integration tests
└── .github/workflows/
    ├── ci.yml                    # Build and test on push/PR
    └── publish.yml               # NuGet publish on version tags
```

## Building from source

```bash
git clone https://github.com/rehmnabdul/dotnet-mcp.git
cd dotnet-mcp
dotnet restore
dotnet build -c Release
dotnet test -c Release
```

Run the sample:

```bash
dotnet run --project samples/TodoApi.Minimal
```

Pack locally:

```bash
dotnet pack src/DotnetMcp.AspNetCore/DotnetMcp.AspNetCore.csproj -c Release -o artifacts
```

## Releases

### `0.1.0-alpha` (2026-07-19)

First public pre-release on NuGet.

**Includes:**

- MCP HTTP server at a configurable route (default `/mcp`)
- Opt-in and opt-out endpoint exposure
- `[McpExpose]`, `[McpIgnore]`, `[McpExposeAll]`, and `.WithMcpExpose()`
- Automatic JSON Schema generation from API Explorer
- In-process endpoint invocation with auth header forwarding
- Sample app and integration tests

**Install:**

```bash
dotnet add package DotnetMcp.AspNetCore --version 0.1.0-alpha
```

**Known limitations (alpha):**

- No stdio transport yet
- No built-in OpenAPI MCP resource
- Symbol packages are not published
- Package license metadata not yet embedded (MIT — see [LICENSE](LICENSE))

### Publishing a new version

Maintainers: bump `Version` in `src/Directory.Build.props`, merge to `main`, then tag and push:

```bash
git tag v0.2.0-alpha
git push origin v0.2.0-alpha
```

The [Publish NuGet](.github/workflows/publish.yml) workflow runs on `v*` tags, uses the `production` GitHub environment, and pushes all three packages to nuget.org.

## Roadmap

- **Phase 2:** stdio transport, auth policy integration, OpenAPI resource
- **Stable 1.0:** API surface review, documentation, and license metadata in packages

## Contributing

1. Fork the repository
2. Create a feature branch
3. Add tests for behavior changes
4. Open a pull request against `main`

CI runs build and tests on every push and pull request to `main`.

## License

MIT — see [LICENSE](LICENSE).
