namespace DotnetMcp.Abstractions;

public sealed class EndpointDescriptor
{
    public string Route { get; set; } = string.Empty;

    public string HttpMethod { get; set; } = string.Empty;

    public string ToolName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool ReadOnly { get; set; }

    public bool Destructive { get; set; }

    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();

    public string? ControllerName { get; set; }

    public string? ActionName { get; set; }

    public Type? DeclaringType { get; set; }
}
