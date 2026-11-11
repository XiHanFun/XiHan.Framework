// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Mcp.Tools;

/// <summary>
/// 发布给 MCP 的一个 tool：名字、对应的操作描述、输入/输出 Schema 与行为提示
/// </summary>
/// <remarks>
/// 通常经 <see cref="Create"/> 从操作描述创建；成员经初始化器给出，以后加字段只加属性（ADR-0142）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record McpTool
{
    /// <summary>
    /// tool 名
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 操作描述
    /// </summary>
    public required OperationDescriptor Descriptor { get; init; }

    /// <summary>
    /// 输入 Schema（JSON 对象文本，<c>type</c> 为 <c>object</c>）
    /// </summary>
    public required string InputSchema { get; init; }

    /// <summary>
    /// 输出 Schema
    /// </summary>
    /// <remarks>
    /// 响应不是对象类型时为 <see langword="null"/>
    /// </remarks>
    public string? OutputSchema { get; init; }

    /// <summary>
    /// 行为提示
    /// </summary>
    public required McpToolAnnotations Annotations { get; init; }

    /// <summary>
    /// 操作没有 Schema 或请求不是对象类型时使用的输入 Schema
    /// </summary>
    public const string DefaultInputSchema = """{"type":"object"}""";

    /// <summary>
    /// 从操作描述创建
    /// </summary>
    /// <remarks>
    /// MCP 要求输入为对象、结构化输出为对象，所以只有 <c>type</c> 为 <c>object</c> 的 Schema 被采用
    /// </remarks>
    public static McpTool Create(string name, OperationDescriptor descriptor)
    {
        McpToolName.Validate(name, nameof(name));
        ArgumentNullException.ThrowIfNull(descriptor);
        var input = descriptor.Schema is { } request && IsObjectSchema(request.Request) ? request.Request : DefaultInputSchema;
        var output = descriptor.Schema is { } response && IsObjectSchema(response.Response) ? response.Response : null;
        return new McpTool { Name = name, Descriptor = descriptor, InputSchema = input, OutputSchema = output, Annotations = McpToolAnnotations.For(descriptor.Contract) };
    }

    /// <summary>
    /// Schema 的 <c>type</c> 是否恰为 <c>object</c>
    /// </summary>
    private static bool IsObjectSchema(string schema)
    {
        using var document = JsonDocument.Parse(schema);
        return document.RootElement.TryGetProperty("type", out var type)
            && type.ValueKind == JsonValueKind.String
            && string.Equals(type.GetString(), "object", StringComparison.Ordinal);
    }
}
