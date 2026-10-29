// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.Abstractions.Pipeline;

/// <summary>
/// 在调用期间补充审计信息。
/// </summary>
/// <remarks>
/// 显式动作（让非操作调用或未声明权限的操作也被记录）、资源名与细节。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class AuditInvocationExtensions
{
    /// <summary>
    /// 显式动作的键。
    /// </summary>
    private static readonly ContextItemKey<string> _actionKey = new("xihan.audit.action");

    /// <summary>
    /// 资源名的键。
    /// </summary>
    private static readonly ContextItemKey<string> _resourceKey = new("xihan.audit.resource");

    /// <summary>
    /// 细节的键。
    /// </summary>
    private static readonly ContextItemKey<Dictionary<string, object?>> _detailsKey = new("xihan.audit.details");

    /// <summary>
    /// 显式声明本次调用的审计动作（如 <c>orders.export</c>）。
    /// </summary>
    /// <remarks>
    /// 声明后无论选择规则如何都会记录。
    /// </remarks>
    public static void SetAuditAction(this InvocationContext context, string action)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        context.Set(_actionKey, action);
    }

    /// <summary>
    /// 读取显式声明的审计动作。
    /// </summary>
    public static Maybe<string> GetAuditAction(this InvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Get(_actionKey);
    }

    /// <summary>
    /// 声明本次调用作用的资源。
    /// </summary>
    public static void SetAuditResource(this InvocationContext context, ResourceName resource)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Set(_resourceKey, resource.ToString());
    }

    /// <summary>
    /// 读取声明的资源名（<c>xrn:</c> 文本）。
    /// </summary>
    public static Maybe<string> GetAuditResource(this InvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Get(_resourceKey);
    }

    /// <summary>
    /// 追加一条细节。
    /// </summary>
    /// <remarks>
    /// 只允许 JSON 可表示的值，同名覆盖。
    /// </remarks>
    public static void AddAuditDetail(this InvocationContext context, string key, object? value)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!context.Get(_detailsKey).TryGetValue(out var details))
        {
            details = new Dictionary<string, object?>(StringComparer.Ordinal);
            context.Set(_detailsKey, details);
        }

        details[key] = value;
    }

    /// <summary>
    /// 读取已追加的细节。
    /// </summary>
    /// <remarks>
    /// 没有为 <see langword="null"/>。
    /// </remarks>
    public static IReadOnlyDictionary<string, object?>? GetAuditDetails(this InvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Get(_detailsKey).TryGetValue(out var details) ? details : null;
    }
}
