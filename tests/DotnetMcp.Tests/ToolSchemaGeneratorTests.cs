using DotnetMcp.Abstractions;
using DotnetMcp.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DotnetMcp.Tests;

public class ToolSchemaGeneratorTests
{
    [Fact]
    public void GenerateInputSchema_Includes_Path_And_Query_Parameters()
    {
        var apiDescription = new ApiDescription
        {
            HttpMethod = "GET",
            RelativePath = "api/items/{id}"
        };

        apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
        {
            Name = "id",
            Source = BindingSource.Path,
            Type = typeof(int),
            IsRequired = true
        });
        apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
        {
            Name = "limit",
            Source = BindingSource.Query,
            Type = typeof(int),
            IsRequired = false
        });

        var schema = new ToolSchemaGenerator().GenerateInputSchema(apiDescription);

        schema.GetProperty("type").GetString().Should().Be("object");
        schema.GetProperty("properties").GetProperty("id").GetProperty("type").GetString().Should().Be("integer");
        schema.GetProperty("properties").GetProperty("limit").GetProperty("type").GetString().Should().Be("integer");
        schema.GetProperty("required").EnumerateArray().Select(value => value.GetString()).Should().Contain("id");
    }
}
