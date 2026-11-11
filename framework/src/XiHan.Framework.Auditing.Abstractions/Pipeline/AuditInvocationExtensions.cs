// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers;
using System.Text.Json;
using System.Text.Json.Nodes;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.Abstractions.Pipeline;

/// <summary>
/// 在调用期间补充审计信息
/// </summary>
/// <remarks>
/// 显式动作（让非操作调用或未声明权限的操作也被记录）、资源名与细节
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class AuditInvocationExtensions
{
    /// <summary>
    /// 显式动作的键
    /// </summary>
    private static readonly ContextItemKey<string> _actionKey = new("xihan.audit.action");

    /// <summary>
    /// 资源名的键
    /// </summary>
    private static readonly ContextItemKey<string> _resourceKey = new("xihan.audit.resource");

    /// <summary>
    /// 细节的键
    /// </summary>
    private static readonly ContextItemKey<Dictionary<string, JsonElement>> _detailsKey = new("xihan.audit.details");

    /// <summary>
    /// 显式声明本次调用的审计动作（如 <c>orders.export</c>）
    /// </summary>
    /// <remarks>
    /// 声明后无论选择规则如何都会记录
    /// </remarks>
    public static void SetAuditAction(this InvocationContext context, string action)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        context.Set(_actionKey, action);
    }

    /// <summary>
    /// 读取显式声明的审计动作
    /// </summary>
    public static Maybe<string> FindAuditAction(this InvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Find(_actionKey);
    }

    /// <summary>
    /// 声明本次调用作用的资源
    /// </summary>
    public static void SetAuditResource(this InvocationContext context, ResourceName resource)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Set(_resourceKey, resource.ToString());
    }

    /// <summary>
    /// 读取声明的资源名（<c>xrn:</c> 文本）
    /// </summary>
    public static Maybe<string> FindAuditResource(this InvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Find(_resourceKey);
    }

    /// <summary>
    /// 追加一条细节
    /// </summary>
    /// <remarks>
    /// 值是 JSON：字符串、数值、布尔、日期与 <see cref="Guid"/> 都能隐式转成 <see cref="JsonNode"/>，<see langword="null"/> 记为 JSON null。追加时即固化成 <see cref="JsonElement"/>，之后改原节点不影响已记下的细节；同名覆盖（ADR-0159）
    /// </remarks>
    public static void AddAuditDetail(this InvocationContext context, string key, JsonNode? value)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!context.Find(_detailsKey).TryGetValue(out var details))
        {
            details = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            context.Set(_detailsKey, details);
        }

        details[key] = Freeze(value);
    }

    /// <summary>
    /// 查找已追加的细节
    /// </summary>
    /// <remarks>
    /// 没有追加过时无值；有值时是此刻的只读副本，之后追加的细节不会出现在里面
    /// </remarks>
    public static Maybe<IReadOnlyDictionary<string, JsonElement>> FindAuditDetails(this InvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // 交出副本：内部字典随调用继续追加，交出原对象会让调用方改到之后写进审计的内容
        return context.Find(_detailsKey).TryGetValue(out var details)
            ? new Dictionary<string, JsonElement>(details, StringComparer.Ordinal).AsReadOnly()
            : Maybe.None<IReadOnlyDictionary<string, JsonElement>>();
    }

    /// <summary>
    /// 把节点固化成脱离文档的 JSON 值
    /// </summary>
    /// <remarks>
    /// 经 UTF-8 写出再解析，不走反射序列化（AOT 可用）
    /// </remarks>
    private static JsonElement Freeze(JsonNode? value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            if (value is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                value.WriteTo(writer);
            }
        }

        // 文档随 using 释放，值要克隆出来才能在之后使用
        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }
}
