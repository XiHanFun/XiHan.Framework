// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using XiHan.Framework.Kernel.Validation;

namespace XiHan.Framework.Kernel.Tests.Validation;

/// <summary>
/// JSON Schema 校验器。
/// </summary>
/// <remarks>
/// 类型、必填、未声明属性、数组、枚举、常量、引用、组合、数值范围、长度与模式、路径与上限。
/// </remarks>
public sealed class JsonSchemaValidatorTests
{
    /// <summary>
    /// 覆盖全部支持关键字的 Schema。
    /// </summary>
    private const string Schema = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "type": "object",
          "properties": {
            "name": { "type": "string", "minLength": 2, "maxLength": 5, "pattern": "^[a-z]+$" },
            "amount": { "type": "integer", "minimum": 1, "exclusiveMaximum": 100 },
            "ratio": { "type": "number", "exclusiveMinimum": 0, "maximum": 1 },
            "note": { "type": ["string", "null"] },
            "status": { "type": "string", "enum": ["open", "closed"] },
            "kind": { "const": "order" },
            "tags": { "type": "array", "items": { "type": "string" }, "minItems": 1, "maxItems": 2 },
            "line": { "$ref": "#/$defs/Line" },
            "first": { "anyOf": [ { "$ref": "#/$defs/Line" }, { "type": "null" } ] },
            "either": { "oneOf": [ { "type": "string" }, { "type": "integer" } ] },
            "both": { "allOf": [ { "type": "integer" }, { "minimum": 10 } ] },
            "prices": { "type": "object", "additionalProperties": { "type": "number" } }
          },
          "required": ["name", "amount"],
          "additionalProperties": false,
          "$defs": { "Line": { "type": "object", "properties": { "sku": { "type": "string" } }, "required": ["sku"] } }
        }
        """;

    /// <summary>
    /// 校验一段 JSON 并返回 <c>路径: 原因</c> 列表。
    /// </summary>
    private static List<string> Check(string json)
    {
        using var schema = JsonDocument.Parse(Schema);
        using var instance = JsonDocument.Parse(json);
        return JsonSchemaValidator.Validate(schema.RootElement, instance.RootElement).Select(static v => v.ToString()).ToList();
    }

    [Fact]
    public void ValidInstance_HasNoViolations()
    {
        Assert.Empty(Check("""{"name":"abc","amount":1,"ratio":0.5,"note":null,"status":"open","kind":"order","tags":["a"],"line":{"sku":"s"},"first":null,"either":"x","both":12,"prices":{"a":1.5}}"""));
        Assert.Empty(Check("""{"name":"ab","amount":99.0}"""));
    }

    [Theory]
    [InlineData("""{"amount":1}""", "$.name: required property 'name' is missing")]
    [InlineData("""{"name":"abc","amount":"1"}""", "$.amount: expected type 'integer' but found string")]
    [InlineData("""{"name":"abc","amount":1.5}""", "$.amount: expected type 'integer' but found number")]
    [InlineData("""{"name":"abc","amount":0}""", "$.amount: value must be >= 1")]
    [InlineData("""{"name":"abc","amount":100}""", "$.amount: value must be < 100")]
    [InlineData("""{"name":"abc","amount":1,"ratio":0}""", "$.ratio: value must be > 0")]
    [InlineData("""{"name":"abc","amount":1,"ratio":1.5}""", "$.ratio: value must be <= 1")]
    [InlineData("""{"name":"a","amount":1}""", "$.name: length must be >= 2")]
    [InlineData("""{"name":"abcdef","amount":1}""", "$.name: length must be <= 5")]
    [InlineData("""{"name":"ab1","amount":1}""", "$.name: value does not match pattern '^[a-z]+$'")]
    [InlineData("""{"name":"abc","amount":1,"note":3}""", "$.note: expected type 'string' or 'null' but found number")]
    [InlineData("""{"name":"abc","amount":1,"status":"gone"}""", "$.status: value is not one of the allowed values")]
    [InlineData("""{"name":"abc","amount":1,"kind":"line"}""", "$.kind: value must be \"order\"")]
    [InlineData("""{"name":"abc","amount":1,"tags":[]}""", "$.tags: must have at least 1 items")]
    [InlineData("""{"name":"abc","amount":1,"tags":["a","b","c"]}""", "$.tags: must have at most 2 items")]
    [InlineData("""{"name":"abc","amount":1,"tags":["a",2]}""", "$.tags[1]: expected type 'string' but found number")]
    [InlineData("""{"name":"abc","amount":1,"line":{}}""", "$.line.sku: required property 'sku' is missing")]
    [InlineData("""{"name":"abc","amount":1,"line":{"sku":1}}""", "$.line.sku: expected type 'string' but found number")]
    [InlineData("""{"name":"abc","amount":1,"first":{"sku":1}}""", "$.first: value matches none of the allowed schemas")]
    [InlineData("""{"name":"abc","amount":1,"either":true}""", "$.either: value must match exactly one of the allowed schemas")]
    [InlineData("""{"name":"abc","amount":1,"both":5}""", "$.both: value must be >= 10")]
    [InlineData("""{"name":"abc","amount":1,"prices":{"a":"x"}}""", "$.prices.a: expected type 'number' but found string")]
    [InlineData("""{"name":"abc","amount":1,"extra":1}""", "$.extra: property is not declared by the contract")]
    [InlineData("""[1]""", "$: expected type 'object' but found array")]
    public void InvalidInstance_ReportsTheViolation(string json, string expected)
    {
        Assert.Equal([expected], Check(json));
    }

    [Fact]
    public void Violations_CarryMessageKeysAndArguments_ForLocalizedFieldErrors()
    {
        using var schema = JsonDocument.Parse(Schema);
        using var instance = JsonDocument.Parse("""{"name":"a","amount":"1","line":{},"tags":[]}""");

        var violations = JsonSchemaValidator.Validate(schema.RootElement, instance.RootElement);

        var name = Assert.Single(violations, static v => v.Path == "$.name");
        Assert.Equal(KernelMessages.ValidationMinLength, name.MessageKey);
        Assert.Equal(2L, name.Arguments!["limit"]);
        Assert.Equal("name", name.Field);
        var amount = Assert.Single(violations, static v => v.Path == "$.amount");
        Assert.Equal(KernelMessages.ValidationType, amount.MessageKey);
        Assert.Equal("'integer'", amount.Arguments!["expected"]);
        Assert.Equal("string", amount.Arguments["actual"]);
        // required 挂在缺失属性自己的路径上，字段键是点分路径
        var sku = Assert.Single(violations, static v => v.Path == "$.line.sku");
        Assert.Equal(KernelMessages.ValidationRequired, sku.MessageKey);
        Assert.Equal("sku", sku.Arguments!["name"]);
        Assert.Equal("line.sku", sku.Field);
        var tags = Assert.Single(violations, static v => v.Path == "$.tags");
        Assert.Equal(KernelMessages.ValidationMinItems, tags.MessageKey);
        Assert.Equal(string.Empty, new SchemaViolation("$", "x").Field);
    }

    [Fact]
    public void Violations_AreCapped_AndUnknownKeywordsAreIgnored()
    {
        using var schema = JsonDocument.Parse("""{"type":"array","items":{"type":"integer","format":"int32","customKeyword":true}}""");
        using var instance = JsonDocument.Parse("[" + string.Join(",", Enumerable.Repeat("\"x\"", JsonSchemaValidator.MaxViolations + 10)) + "]");

        var violations = JsonSchemaValidator.Validate(schema.RootElement, instance.RootElement);

        Assert.Equal(JsonSchemaValidator.MaxViolations, violations.Count);
        Assert.Equal("$[0]", violations[0].Path);
    }

    [Fact]
    public void BooleanSchemas_AndUnresolvableReferences_AreHandled()
    {
        using var accept = JsonDocument.Parse("true");
        using var reject = JsonDocument.Parse("false");
        using var dangling = JsonDocument.Parse("""{"$ref":"#/$defs/Missing"}""");
        using var instance = JsonDocument.Parse("1");

        Assert.Empty(JsonSchemaValidator.Validate(accept.RootElement, instance.RootElement));
        Assert.Equal(["$: no value is allowed here"], JsonSchemaValidator.Validate(reject.RootElement, instance.RootElement).Select(static v => v.ToString()));
        Assert.Equal(["$: schema reference '#/$defs/Missing' cannot be resolved"], JsonSchemaValidator.Validate(dangling.RootElement, instance.RootElement).Select(static v => v.ToString()));
        Assert.Throws<ArgumentException>(static () => new SchemaViolation(" ", "x"));
        Assert.Throws<ArgumentException>(static () => new SchemaViolation("$", ""));
    }
}
