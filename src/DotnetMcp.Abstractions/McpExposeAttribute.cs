namespace DotnetMcp.Abstractions;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class McpExposeAttribute : Attribute
{
    public string? Name { get; set; }

    public string? Description { get; set; }

    public bool ReadOnly { get; set; }

    public bool Destructive { get; set; }

    public string[] Roles { get; set; } = Array.Empty<string>();
}
