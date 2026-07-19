using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DotnetMcp;

public sealed class ToolSchemaGenerator
{
    public JsonElement GenerateInputSchema(ApiDescription apiDescription)
    {
        var properties = new JsonObject();
        var required = new JsonArray();

        foreach (var parameter in apiDescription.ParameterDescriptions)
        {
            if (parameter.Source == BindingSource.Services)
            {
                continue;
            }

            properties[parameter.Name] = CreatePropertySchema(parameter.Type, parameter.IsRequired);
            if (parameter.IsRequired)
            {
                required.Add(parameter.Name);
            }
        }

        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties
        };

        if (required.Count > 0)
        {
            schema["required"] = required;
        }

        return JsonSerializer.SerializeToElement(schema);
    }

    private static JsonObject CreatePropertySchema(Type type, bool isRequired)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        if (underlyingType.IsEnum)
        {
            var enumValues = new JsonArray();
            foreach (var name in Enum.GetNames(underlyingType))
            {
                enumValues.Add(name);
            }

            return new JsonObject
            {
                ["type"] = "string",
                ["enum"] = enumValues
            };
        }

        if (underlyingType == typeof(string) ||
            underlyingType == typeof(Guid) ||
            underlyingType == typeof(DateTime) ||
            underlyingType == typeof(DateTimeOffset) ||
            underlyingType == typeof(TimeSpan))
        {
            return new JsonObject { ["type"] = "string" };
        }

        if (underlyingType == typeof(bool))
        {
            return new JsonObject { ["type"] = "boolean" };
        }

        if (underlyingType == typeof(int) ||
            underlyingType == typeof(long) ||
            underlyingType == typeof(short) ||
            underlyingType == typeof(byte))
        {
            return new JsonObject { ["type"] = "integer" };
        }

        if (underlyingType == typeof(float) ||
            underlyingType == typeof(double) ||
            underlyingType == typeof(decimal))
        {
            return new JsonObject { ["type"] = "number" };
        }

        if (IsSimpleObject(underlyingType))
        {
            return CreateObjectSchema(underlyingType);
        }

        return new JsonObject
        {
            ["type"] = "string",
            ["description"] = $"JSON-encoded {underlyingType.Name}"
        };
    }

    private static JsonObject CreateObjectSchema(Type type)
    {
        var properties = new JsonObject();
        var required = new JsonArray();

        foreach (var property in type.GetProperties())
        {
            if (!property.CanRead)
            {
                continue;
            }

            properties[property.Name] = CreatePropertySchema(property.PropertyType, isRequired: false);
        }

        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties
        };

        if (required.Count > 0)
        {
            schema["required"] = required;
        }

        return schema;
    }

    private static bool IsSimpleObject(Type type)
    {
        return type.IsClass &&
               type != typeof(string) &&
               type.GetProperties().Length <= 8 &&
               type.GetProperties().All(p => IsSimpleType(Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType));
    }

    private static bool IsSimpleType(Type type)
    {
        return type.IsPrimitive ||
               type.IsEnum ||
               type == typeof(string) ||
               type == typeof(decimal) ||
               type == typeof(Guid) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan);
    }
}
