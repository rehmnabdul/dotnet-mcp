# dotnet-mcp

A .NET library that exposes your ASP.NET Core APIs as an [MCP (Model Context Protocol)](https://modelcontextprotocol.io/) server. AI assistants and agents can discover your endpoints as MCP tools, inspect JSON Schema for parameters, and invoke them over HTTP with minimal configuration.

**Current release:** [`1.0.0-rc.1`](https://www.nuget.org/packages/DotnetMcp.AspNetCore/1.0.0-rc.1) (release candidate)

## Features

- **Automatic tool discovery** — uses ASP.NET Core API Explorer to discover Minimal API and controller endpoints
- **Opt-in or opt-out exposure** — expose only annotated endpoints, or expose everything except ignored ones
- **JSON Schema generation** — tool input schemas are built from route, query, and body parameters
- **In-process invocation** — MCP tool calls dispatch through the same endpoint pipeline as normal HTTP requests
- **HTTP and stdio transports** — streamable HTTP/SSE for remote hosts, or stdin/stdout for local IDE/CLI agents
- **OpenAPI MCP resource** — generated OpenAPI 3 document for exposed endpoints via `resources/list` / `resources/read`
- **Auth enforcement** — `[Authorize]`, policies, roles, and `[McpExpose(Roles = ...)]` are checked on tool calls
- **Auth forwarding** — `Authorization` headers and the authenticated `User` from the MCP request are forwarded to invoked endpoints

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
dotnet add package DotnetMcp.AspNetCore --version 1.0.0-rc.1
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

## Stdio transport (local agents)

For IDE / CLI MCP hosts that launch a local process, use stdin/stdout instead of HTTP:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.UseDotnetMcpStdioLogging(); // logs must go to stderr
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

app.MapDotnetMcp(); // no-op for stdio; kept for shared startup code
app.Run();
```

Sample:

```bash
dotnet run --project samples/TodoApi.Stdio
```

Example MCP host config (Cursor / Claude Desktop style):

```json
{
  "mcpServers": {
    "todo-api": {
      "command": "dotnet",
      "args": ["run", "--project", "/absolute/path/to/samples/TodoApi.Stdio"]
    }
  }
}
```

## Auth policy sample

`samples/TodoApi.Auth` shows JWT Bearer auth with ASP.NET policies and MCP role checks:

```bash
dotnet run --project samples/TodoApi.Auth
```

1. Mint a token:

```bash
curl -s http://localhost:5000/auth/token \
  -H 'Content-Type: application/json' \
  -d '{"username":"ada","role":"Admin"}'
```

2. Call MCP tools with `Authorization: Bearer <token>` on the MCP HTTP client.

What it demonstrates:

| Endpoint | ASP.NET policy | MCP extra |
|----------|----------------|-----------|
| `get_todo` | `TodosRead` (authenticated) | — |
| `create_todo` / `delete_todo` | `TodosAdmin` (Admin role) | — |
| `get_analytics_summary` | `[AllowAnonymous]` | `Roles = ["Analyst"]` on MCP |

`.WithMcpExpose(..., roles: ["Analyst"])` enforces roles even when the HTTP endpoint allows anonymous access.

## OpenAPI MCP resource

By default, DotnetMcp exposes a generated OpenAPI 3 document for MCP-exposed endpoints:

```csharp
var resources = await client.ListResourcesAsync();
var openApi = await client.ReadResourceAsync("openapi://dotnet-mcp/document");
```

Disable or customize:

```csharp
builder.Services.AddDotnetMcp(options =>
{
    options.EnableOpenApiResource = true;
    options.OpenApiResourceUri = "openapi://myapp/v1";
    options.OpenApiTitle = "My App API";
    options.OpenApiVersion = "1.0.0-rc.1";
});
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

1. **`AddDotnetMcp()`** registers discovery, schema generation, and MCP server handlers for HTTP or stdio.
2. **`MapDotnetMcp("/mcp")`** maps the MCP HTTP endpoint (skipped automatically when `Transport = Stdio`).
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
| `ExposureMode` | `OptIn` | `OptIn` exposes only annotated endpoints; `OptOut` exposes all except ignored |
| `RequireExplicitAnnotation` | `false` | When `OptOut`, require `[McpExpose]` / `[McpExposeAll]` / `.WithMcpExpose()` |
| `MapHttpMethods` | `true` | Prefix auto-generated tool names with the HTTP method (e.g. `get_users_by_id`) |
| `ToolNamePrefix` | `""` | Prefix applied to all tool names |
| `ExcludePaths` | `[]` | Path prefixes to never expose (case-insensitive) |
| `McpRoutePattern` | `"/mcp"` | Documented default route pattern (map with `MapDotnetMcp`) |
| `Filter` | `null` | Additional predicate over `EndpointDescriptor` |
| `EnforceEndpointAuthorization` | `true` | Enforce `[Authorize]` / roles on MCP tool calls |
| `Transport` | `Http` | `Http` for `MapDotnetMcp`, or `Stdio` for local stdin/stdout hosts |
| `EnableOpenApiResource` | `true` | Expose a generated OpenAPI 3 document as an MCP resource |
| `OpenApiResourceUri` | `openapi://dotnet-mcp/document` | URI used in `resources/list` and `resources/read` |
| `OpenApiTitle` / `OpenApiVersion` | `dotnet-mcp` / `1.0.0` | OpenAPI `info` fields |

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

### Authorization

MCP tool calls run outside the normal HTTP middleware pipeline for the target endpoint, so authorization is enforced explicitly before invocation:

1. Endpoint `[Authorize]` / policy / role metadata (unless `[AllowAnonymous]`)
2. `[McpExpose(Roles = ...)]` / `.WithMcpExpose` role requirements (always enforced when set)
3. The MCP request's authenticated `User` and `Authorization` header are copied onto the in-process call

```csharp
[HttpGet("admin/report")]
[Authorize(Roles = "Admin")]
[McpExpose(Name = "get_admin_report", Description = "Admin-only report")]
public IActionResult AdminReport() => Ok(new { ok = true });

// Or require roles only at the MCP layer:
[HttpGet("stats")]
[AllowAnonymous]
[McpExpose(Name = "get_stats", Roles = new[] { "Analyst" })]
public IActionResult Stats() => Ok();
```

Your app still needs standard ASP.NET Core auth setup (`AddAuthentication`, `AddAuthorization`, `UseAuthentication`).

To disable enforcement (not recommended):

```csharp
builder.Services.AddDotnetMcp(options =>
{
    options.EnforceEndpointAuthorization = false;
});
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

The MCP request's `Authorization` header and authenticated `User` are forwarded to the invoked endpoint. Authorization metadata is evaluated before invocation (see [Authorization](#authorization)).

## Project structure

```
dotnet-mcp/
├── src/
│   ├── DotnetMcp.Abstractions/   # Attributes, options, contracts
│   ├── DotnetMcp/                # Discovery, filtering, schemas (NuGet: DotnetMcp.Core)
│   └── DotnetMcp.AspNetCore/     # MCP server wiring and invocation
├── samples/
│   ├── TodoApi.Minimal/          # HTTP MCP sample
│   ├── TodoApi.Stdio/            # stdio MCP sample
│   └── TodoApi.Auth/             # JWT + policy sample
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

Run samples:

```bash
dotnet run --project samples/TodoApi.Minimal
dotnet run --project samples/TodoApi.Stdio
dotnet run --project samples/TodoApi.Auth
```

Pack locally:

```bash
dotnet pack src/DotnetMcp.AspNetCore/DotnetMcp.AspNetCore.csproj -c Release -o artifacts
```

## Releases

### `1.0.0-rc.1`

Release candidate toward stable 1.0.

**Includes / changes:**

- Default `ExposureMode` is now **`OptIn`** (breaking vs earlier alphas that defaulted to `OptOut`)
- Auth policy sample (`samples/TodoApi.Auth`) with JWT + ASP.NET policies + MCP roles
- `.WithMcpExpose(..., roles:)` support for Minimal APIs
- Phase 2 features: auth enforcement, stdio, OpenAPI resource

**Install:**

```bash
dotnet add package DotnetMcp.AspNetCore --version 1.0.0-rc.1
```

### `0.1.3-alpha`

OpenAPI document as an MCP resource.

**Includes:**

- Generated OpenAPI 3.0 document from MCP-exposed endpoints
- `resources/list` + `resources/read` handlers (`openapi://dotnet-mcp/document`)
- Options: `EnableOpenApiResource`, `OpenApiResourceUri`, `OpenApiTitle`, `OpenApiVersion`

**Install:**

```bash
dotnet add package DotnetMcp.AspNetCore --version 0.1.3-alpha
```

### `0.1.2-alpha`

Stdio transport for local MCP hosts.

**Includes:**

- `McpTransportMode.Stdio` / `DotnetMcpOptions.Transport`
- `UseDotnetMcpStdioLogging()` helper (stderr logging)
- `samples/TodoApi.Stdio` and stdio integration test
- `MapDotnetMcp` is a no-op when transport is stdio

**Install:**

```bash
dotnet add package DotnetMcp.AspNetCore --version 0.1.2-alpha
```

### `0.1.1-alpha`

Package metadata fix + authorization enforcement.

**Includes:**

- NuGet packages now carry MIT license, authors, project/repository URLs, and symbol packages (`.snupkg`)
- MCP tool calls enforce `[Authorize]`, policies/roles, and `[McpExpose(Roles = ...)]`
- Authenticated `User` is forwarded with the in-process invocation
- `EnforceEndpointAuthorization` option (default `true`)

**Install:**

```bash
dotnet add package DotnetMcp.AspNetCore --version 0.1.1-alpha
```

### `0.1.0-alpha` (2026-07-19)

First public pre-release on NuGet.

**Includes:**

- MCP HTTP server at a configurable route (default `/mcp`)
- Opt-in and opt-out endpoint exposure
- `[McpExpose]`, `[McpIgnore]`, `[McpExposeAll]`, and `.WithMcpExpose()`
- Automatic JSON Schema generation from API Explorer
- In-process endpoint invocation with auth header forwarding
- Sample app and integration tests

### Publishing a new version

Maintainers: bump `Version` in root `Directory.Build.props`, merge to `main`, then tag and push:

```bash
git tag v1.0.0-rc.1
git push origin v1.0.0-rc.1
```

The [Publish NuGet](.github/workflows/publish.yml) workflow runs on `v*` tags, uses the `production` GitHub environment, and pushes all three packages to nuget.org.

## Public API (1.0 surface)

Stable entry points for application authors:

| API | Purpose |
|-----|---------|
| `AddDotnetMcp(Action<DotnetMcpOptions>?)` | Register discovery, MCP handlers, transport |
| `MapDotnetMcp(string pattern = "/mcp")` | Map HTTP MCP endpoint (no-op for stdio) |
| `UseDotnetMcpStdioLogging()` | stderr logging for stdio hosts |
| `.WithMcpExpose(...)` | Expose Minimal API endpoints |
| `[McpExpose]` / `[McpIgnore]` / `[McpExposeAll]` | Controller annotations |
| `DotnetMcpOptions` | Exposure, transport, auth, OpenAPI settings |

## Roadmap

- **`1.0.0`:** finalize RC feedback, then tag stable without `-rc`
- Optional later: multi-document OpenAPI, richer resource templates

## Contributing

1. Fork the repository
2. Create a feature branch
3. Add tests for behavior changes
4. Open a pull request against `main`

CI runs build and tests on every push and pull request to `main`.

## License

MIT — see [LICENSE](LICENSE).
