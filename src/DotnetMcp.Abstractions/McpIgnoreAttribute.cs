namespace DotnetMcp.Abstractions;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class McpIgnoreAttribute : Attribute
{
}
