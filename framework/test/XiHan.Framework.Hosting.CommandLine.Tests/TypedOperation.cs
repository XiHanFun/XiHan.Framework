// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.CommandLine.Tests;

/// <summary>
/// 带请求 Schema 的回显操作
/// </summary>
/// <remarks>
/// 原样返回解码后的请求，用来观察字段选项写成了什么类型。Schema 与 <c>Kernel.Generators</c> 的产出同形，<c>additionalProperties</c> 另外验证未声明属性也按 Schema 转换
/// </remarks>
internal sealed class TypedOperation : IOperation<TypedRequest, TypedRequest>
{
    /// <summary>
    /// 请求 Schema
    /// </summary>
    public const string RequestSchema = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "TypedRequest",
          "type": "object",
          "properties": {
            "name": { "type": "string" },
            "times": { "type": "integer", "format": "int32", "minimum": 1, "maximum": 10 },
            "total": { "type": ["integer", "null"], "format": "int64" },
            "ratio": { "type": ["number", "null"], "format": "double" },
            "verbose": { "type": "boolean" },
            "level": { "anyOf": [{ "$ref": "#/$defs/Level" }, { "type": "null" }] },
            "tone": { "anyOf": [{ "$ref": "#/$defs/Tone" }, { "type": "null" }] },
            "sizes": { "type": ["array", "null"], "items": { "type": "integer", "format": "int32" } },
            "tags": { "type": ["array", "null"], "items": { "type": "string" } },
            "notes": {}
          },
          "additionalProperties": { "type": "integer" },
          "required": ["name"],
          "$defs": {
            "Level": { "type": "integer", "format": "int32" },
            "Tone": { "type": "string", "enum": ["warm", "cold"] }
          }
        }
        """;

    /// <summary>
    /// 响应 Schema
    /// </summary>
    public const string ResponseSchema = """{"type":"object"}""";

    /// <inheritdoc />
    public ValueTask<Result<TypedRequest>> HandleAsync(TypedRequest request, InvocationContext context)
        => ValueTask.FromResult(Result.Success(request));
}
