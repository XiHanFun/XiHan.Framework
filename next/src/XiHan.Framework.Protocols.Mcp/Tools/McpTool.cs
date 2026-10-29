// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Mcp.Tools;

/// <summary>
/// 发布给 MCP 的一个 tool：名字、对应的操作描述、输入/输出 Schema 与行为提示。
/// </summary>
/// <param name="Name">tool 名。</param>
/// <param name="Descriptor">操作描述。</param>
/// <param name="InputSchema">输入 Schema（JSON 对象文本，<c>type</c> 为 <c>object</c>）。</param>
/// <param name="OutputSchema">输出 Schema；响应不是对象类型时为 <see langword="null"/>。</param>
/// <param name="Annotations">行为提示。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record McpTool(string Name, OperationDescriptor Descriptor, string InputSchema, string? OutputSchema, McpToolAnnotations Annotations)
{
    /// <summary>
    /// 操作没有 Schema 或请求不是对象类型时使用的输入 Schema。
    /// </summary>
    public const string DefaultInputSchema = """{"type":"object"}""";

    /// <summary>
    /// 从操作描述创建。
    /// </summary>
    /// <remarks>
    /// MCP 要求输入为对象、结构化输出为对象，所以只有 <c>type</c> 为 <c>object</c> 的 Schema 被采用。
    /// </remarks>
    public static McpTool Create(string name, OperationDescriptor descriptor)
    {
        McpToolName.Validate(name, nameof(name));
        ArgumentNullException.ThrowIfNull(descriptor);
        var input = descriptor.Schema is { } request && IsObjectSchema(request.Request) ? request.Request : DefaultInputSchema;
        var output = descriptor.Schema is { } response && IsObjectSchema(response.Response) ? response.Response : null;
        return new McpTool(name, descriptor, input, output, McpToolAnnotations.For(descriptor.Contract));
    }

    /// <summary>
    /// Schema 的 <c>type</c> 是否恰为 <c>object</c>。
    /// </summary>
    private static bool IsObjectSchema(string schema)
    {
        using var document = JsonDocument.Parse(schema);
        return document.RootElement.TryGetProperty("type", out var type)
            && type.ValueKind == JsonValueKind.String
            && string.Equals(type.GetString(), "object", StringComparison.Ordinal);
    }
}
