namespace DotnetMcp.Abstractions;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class McpExposeAllAttribute : Attribute
{
}
