# DotnetMcp

[![NuGet](https://img.shields.io/nuget/v/DotnetMcp.AspNetCore.svg)](https://www.nuget.org/packages/DotnetMcp.AspNetCore)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Expose ASP.NET Core APIs as an [MCP (Model Context Protocol)](https://modelcontextprotocol.io/) server. AI assistants and agents discover your endpoints as tools, get JSON Schema for parameters, and invoke them over **HTTP** or **stdio** — with the same authorization rules as your normal API.

**Current release:** [`1.0.0`](https://www.nuget.org/packages/DotnetMcp.AspNetCore/1.0.0)

## Contents

- [Features](#features)
- [Packages](#packages)
- [Requirements](#requirements)
- [Quick start](#quick-start)
- [Transports](#transports)
- [Authorization](#authorization)
- [OpenAPI resource](#openapi-resource)
- [Configuration](#configuration)
- [Exposing endpoints](#exposing-endpoints)
- [Tool naming & schemas](#tool-naming--schemas)
- [How it works](#how-it-works)
- [Troubleshooting](#troubleshooting)
- [Building from source](#building-from-source)
- [Releases](#releases)
- [Public API](#public-api-10-surface)
- [Roadmap](#roadmap)
- [Contributing](#contributing)
- [License](#license)

## Features

- **Automatic discovery** — Minimal APIs and controllers via API Explorer
- **Opt-in by default** — only annotated endpoints become tools (`OptOut` available)
- **JSON Schema inputs** — from route, query, and body parameters
- **In-process invocation** — tool calls go through the same endpoint handlers as HTTP
- **HTTP + stdio** — remote hosts over streamable HTTP, or local IDE/CLI agents over stdin/stdout
- **OpenAPI MCP resource** — generated OpenAPI 3 doc for exposed endpoints
- **Auth enforcement** — `[Authorize]`, policies, roles, and MCP-only role hints
- **Auth forwarding** — `Authorization` header and authenticated `User` are copied onto invocations

## Packages

| Package | NuGet | Description |
|---------|-------|-------------|
| `DotnetMcp.AspNetCore` | [nuget.org](https://www.nuget.org/packages/DotnetMcp.AspNetCore) | **Install this** — ASP.NET Core integration and MCP endpoint |
| `DotnetMcp.Core` | [nuget.org](https://www.nuget.org/packages/DotnetMcp.Core) | Discovery, filtering, schema generation, tool naming |
| `DotnetMcp.Abstractions` | [nuget.org](https://www.nuget.org/packages/DotnetMcp.Abstractions) | Attributes, options, and contracts |

> The middle package is published as `DotnetMcp.Core` because the `DotnetMcp` package ID is already taken on nuget.org.

## Requirements

- .NET 8.0+
- ASP.NET Core with API Explorer enabled:
  - Minimal APIs: `AddEndpointsApiExplorer()`
  - Controllers: `AddControllers()` or `AddMvcCore().AddApiExplorer()`

## Quick start

### 1. Install

```bash
dotnet add package DotnetMcp.AspNetCore --version 1.0.0
```

### 2. Register and map

```csharp
using DotnetMcp.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddDotnetMcp(); // ExposureMode defaults to OptIn

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

### 3. Connect a client

MCP is available at `/mcp` by default. Example with the official .NET MCP client:

```csharp
using ModelContextProtocol.Client;

var httpClient = new HttpClient { BaseAddress = new Uri("https://localhost:5001") };
var transport = new HttpClientTransport(
    new HttpClientTransportOptions
    {
        Endpoint = new Uri(httpClient.BaseAddress!, "/mcp"),
        TransportMode = HttpTransportMode.StreamableHttp
    },
    httpClient);

await using var client = await McpClient.CreateAsync(transport);

var tools = await client.ListToolsAsync();
var result = await client.CallToolAsync("get_todo", new Dictionary<string, object?> { ["id"] = 1 });
```

Try the sample:

```bash
dotnet run --project samples/TodoApi.Minimal
```

## Transports

| Transport | When to use | How to enable |
|-----------|-------------|---------------|
| **HTTP** (default) | Remote agents, web apps, shared hosts | `MapDotnetMcp("/mcp")` |
| **Stdio** | Local IDE / CLI hosts (Cursor, Claude Desktop, etc.) | `options.Transport = McpTransportMode.Stdio` |

### HTTP (default)

No extra transport config — map the endpoint and point an MCP client at it.

### Stdio (local agents)

Logs must go to **stderr** so stdout stays clean for the MCP protocol:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.UseDotnetMcpStdioLogging();
builder.WebHost.UseUrls("http://127.0.0.1:0");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddDotnetMcp(options =>
{
    options.Transport = McpTransportMode.Stdio;
});

var app = builder.Build();

app.MapGet("/api/todos/{id}", (int id) => Results.Ok(new { id, title = "Learn MCP" }))
   .WithMcpExpose("get_todo", "Get todo by id", readOnly: true);

app.MapDotnetMcp(); // no-op for stdio; safe to keep for shared startup
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

## Authorization

MCP tool calls are invoked in-process, outside the target endpoint’s normal HTTP middleware pipeline. DotnetMcp enforces authorization explicitly before invocation:

1. Endpoint `[Authorize]` / policy / role metadata (unless `[AllowAnonymous]`)
2. `[McpExpose(Roles = ...)]` / `.WithMcpExpose(..., roles:)` (always enforced when set)
3. The MCP request’s authenticated `User` and `Authorization` header are copied onto the in-process call

```csharp
[HttpGet("admin/report")]
[Authorize(Roles = "Admin")]
[McpExpose(Name = "get_admin_report", Description = "Admin-only report")]
public IActionResult AdminReport() => Ok(new { ok = true });

// MCP-only roles even when HTTP allows anonymous:
[HttpGet("stats")]
[AllowAnonymous]
[McpExpose(Name = "get_stats", Roles = new[] { "Analyst" })]
public IActionResult Stats() => Ok();
```

Your app still needs standard ASP.NET Core auth (`AddAuthentication`, `AddAuthorization`, `UseAuthentication`).

To disable enforcement (not recommended):

```csharp
builder.Services.AddDotnetMcp(options =>
{
    options.EnforceEndpointAuthorization = false;
});
```

### Auth policy sample

`samples/TodoApi.Auth` demonstrates JWT Bearer auth with ASP.NET policies and MCP roles:

```bash
dotnet run --project samples/TodoApi.Auth
```

Mint a token:

```bash
curl -s http://localhost:5000/auth/token \
  -H 'Content-Type: application/json' \
  -d '{"username":"ada","role":"Admin"}'
```

Then call MCP tools with `Authorization: Bearer <token>` on the MCP HTTP client.

| Tool | ASP.NET policy | MCP extra |
|------|----------------|-----------|
| `get_todo` | `TodosRead` (authenticated) | — |
| `create_todo` / `delete_todo` | `TodosAdmin` (Admin role) | — |
| `get_analytics_summary` | `[AllowAnonymous]` | `Roles = ["Analyst"]` on MCP |

## OpenAPI resource

By default, DotnetMcp exposes a generated OpenAPI 3 document for MCP-exposed endpoints:

```csharp
var resources = await client.ListResourcesAsync();
var openApi = await client.ReadResourceAsync("openapi://dotnet-mcp/document");
```

Customize or disable:

```csharp
builder.Services.AddDotnetMcp(options =>
{
    options.EnableOpenApiResource = true;
    options.OpenApiResourceUri = "openapi://myapp/v1";
    options.OpenApiTitle = "My App API";
    options.OpenApiVersion = "1.0.0";
});
```

## Configuration

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
| `ExposureMode` | `OptIn` | `OptIn` = annotated only; `OptOut` = all except ignored |
| `RequireExplicitAnnotation` | `false` | When `OptOut`, still require explicit expose annotations |
| `MapHttpMethods` | `true` | Prefix auto-generated names with HTTP method (e.g. `get_users_by_id`) |
| `ToolNamePrefix` | `""` | Prefix applied to all tool names |
| `ExcludePaths` | `[]` | Path prefixes never exposed (case-insensitive) |
| `McpRoutePattern` | `"/mcp"` | Documented default route (map with `MapDotnetMcp`) |
| `Filter` | `null` | Extra predicate over `EndpointDescriptor` |
| `EnforceEndpointAuthorization` | `true` | Enforce `[Authorize]` / roles on tool calls |
| `Transport` | `Http` | `Http` or `Stdio` |
| `EnableOpenApiResource` | `true` | Expose generated OpenAPI 3 as an MCP resource |
| `OpenApiResourceUri` | `openapi://dotnet-mcp/document` | URI for `resources/list` / `resources/read` |
| `OpenApiTitle` / `OpenApiVersion` | `dotnet-mcp` / `1.0.0` | OpenAPI `info` fields |

## Exposing endpoints

### Exposure modes

**Opt-in (default — recommended for production)**

Only endpoints you mark are tools:

```csharp
options.ExposureMode = McpExposureMode.OptIn;
```

**Opt-out**

Expose everything discovered except ignored/excluded paths:

```csharp
options.ExposureMode = McpExposureMode.OptOut;
options.ExcludePaths = new[] { "health", "internal/" };
```

### Minimal APIs — `.WithMcpExpose()`

```csharp
app.MapDelete("/api/todos/{id}", (int id) => Results.NoContent())
   .WithMcpExpose("delete_todo", "Delete a todo", destructive: true);

app.MapGet("/api/analytics/summary", () => Results.Ok())
   .WithMcpExpose("get_analytics_summary", "Analytics summary", roles: new[] { "Analyst" });
```

| Parameter | Description |
|-----------|-------------|
| `name` | Explicit MCP tool name (recommended) |
| `description` | Shown to clients in `tools/list` |
| `readOnly` | MCP read-only hint (defaults from GET/HEAD) |
| `destructive` | MCP destructive hint (defaults to `true` for DELETE) |
| `roles` | Extra roles required for MCP calls (even if HTTP is anonymous) |

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

### Expose all actions — `[McpExposeAll]`

```csharp
[McpExposeAll]
[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    // All actions are exposed unless individually ignored
}
```

## Tool naming & schemas

### Naming

When no explicit name is provided, names come from the HTTP method and route:

| Route | HTTP | Generated tool name |
|-------|------|---------------------|
| `/api/users/{id}` | GET | `get_users_by_id` |
| `/api/users` | POST | `post_users` |
| `/api/todos/{id}` | DELETE | `delete_todos_by_id` |

Rules:

- HTTP method included when `MapHttpMethods` is `true`
- Path segments lowercased; `-` → `_`
- `api` segment skipped
- Route parameters become `by_{param}` (e.g. `{id}` → `by_id`)
- `ToolNamePrefix` prepended when set

### Input schema

API Explorer parameters become JSON Schema properties:

- **Route** → required/optional from model metadata
- **Query** → query string on invocation
- **Body** → JSON body (single param or composite object)
- Types: `string`, `bool`, integers, `decimal`/`double`/`float`, enums, arrays, nested objects

Example for `GET /api/users/{id}`:

```json
{
  "type": "object",
  "properties": {
    "id": { "type": "string" }
  },
  "required": ["id"]
}
```

### Invocation

On `tools/call`:

1. Route parameters substituted into the path
2. Query parameters appended
3. Remaining arguments serialized as JSON body for POST/PUT/PATCH
4. Matching endpoint invoked in-process
5. Response body returned as MCP text content
6. HTTP status ≥ 400 reported as tool errors

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

1. **`AddDotnetMcp()`** registers discovery, schema generation, and MCP handlers.
2. **`MapDotnetMcp("/mcp")`** maps the HTTP endpoint (skipped when `Transport = Stdio`).
3. **`tools/list`** returns exposed endpoints as tools with JSON Schema inputs.
4. **`tools/call`** maps arguments to route/query/body and invokes the endpoint in-process.

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| No tools in `tools/list` | API Explorer not registered, or nothing annotated under `OptIn` | Call `AddEndpointsApiExplorer()` / enable controller API Explorer; add `.WithMcpExpose` / `[McpExpose]` |
| Expected endpoint missing | Path excluded, `[McpIgnore]`, or filter rejected it | Check `ExcludePaths`, attributes, and `Filter` |
| `401` / `403` on tool call | Auth not configured or roles don’t match | Set up ASP.NET auth; send `Authorization` on the MCP client; check policies / `roles:` |
| Stdio host hangs or misparses | Logs written to stdout | Use `UseDotnetMcpStdioLogging()` so logs go to stderr |
| Client can’t connect over HTTP | Wrong URL or transport mode | Use `/mcp` (or your mapped path) with streamable HTTP |

## Building from source

```bash
git clone https://github.com/rehmnabdul/dotnet-mcp.git
cd dotnet-mcp
dotnet restore
dotnet build -c Release
dotnet test -c Release
```

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

```bash
dotnet run --project samples/TodoApi.Minimal
dotnet run --project samples/TodoApi.Stdio
dotnet run --project samples/TodoApi.Auth

dotnet pack src/DotnetMcp.AspNetCore/DotnetMcp.AspNetCore.csproj -c Release -o artifacts
```

## Releases

### `1.0.0` (stable)

First stable release on **ModelContextProtocol.AspNetCore 1.4.1**.

- Opt-in-by-default exposure (`McpExposureMode.OptIn`)
- HTTP (streamable) and stdio transports
- Authorization enforcement + `.WithMcpExpose(..., roles:)`
- OpenAPI 3 document as an MCP resource
- Samples: Minimal, Stdio, Auth

```bash
dotnet add package DotnetMcp.AspNetCore --version 1.0.0
```

**Breaking vs early alphas:** default `ExposureMode` is `OptIn` (was `OptOut` before `1.0.0-rc.1`).

### Previous pre-releases

| Version | Highlights |
|---------|------------|
| `1.0.0-rc.1` | OptIn default, auth sample, MCP roles on Minimal APIs |
| `0.1.3-alpha` | OpenAPI MCP resource |
| `0.1.2-alpha` | Stdio transport |
| `0.1.1-alpha` | Package metadata + auth enforcement |
| `0.1.0-alpha` | First public NuGet pre-release |

### Publishing a new version

Maintainers: bump `Version` in `Directory.Build.props`, merge to `main`, then:

```bash
git tag v1.1.0
git push origin v1.1.0
```

The [Publish NuGet](.github/workflows/publish.yml) workflow runs on `v*` tags (GitHub `production` environment) and pushes all three packages to nuget.org.

## Public API (1.0 surface)

| API | Purpose |
|-----|---------|
| `AddDotnetMcp(Action<DotnetMcpOptions>?)` | Register discovery, MCP handlers, transport |
| `MapDotnetMcp(string pattern = "/mcp")` | Map HTTP MCP endpoint (no-op for stdio) |
| `UseDotnetMcpStdioLogging()` | stderr logging for stdio hosts |
| `.WithMcpExpose(...)` | Expose Minimal API endpoints |
| `[McpExpose]` / `[McpIgnore]` / `[McpExposeAll]` | Controller annotations |
| `DotnetMcpOptions` | Exposure, transport, auth, OpenAPI settings |

## Roadmap

- **1.0.x** — bugfix and docs on the stable line
- Later (additive): multi-document OpenAPI, OIDC/Entra sample, richer resource templates

## Contributing

1. Fork the repository
2. Create a feature branch
3. Add tests for behavior changes
4. Open a pull request against `main`

CI builds and tests every push and PR to `main`.

## License

MIT — see [LICENSE](LICENSE).
