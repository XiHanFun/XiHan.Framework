// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Pipeline;

/// <summary>
/// 调用上下文的强类型 Items 访问与调用结果槽
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class InvocationContextExtensions
{
    /// <summary>
    /// 调用结果在 <see cref="InvocationContext.Items"/> 中的键
    /// </summary>
    private static readonly ContextItemKey<Result> _outcomeKey = new("xihan.outcome");

    /// <summary>
    /// 调用来源在 <see cref="InvocationContext.Items"/> 中的键
    /// </summary>
    private static readonly ContextItemKey<InvocationOrigin> _originKey = new("xihan.origin");

    /// <summary>
    /// 这次调用写往下游的 <c>traceparent</c>
    /// </summary>
    /// <remarks>
    /// RFC-0003 规则 7「写出」：当前 Activity 是这次调用的 Span（与调用同一 trace，且有采样决定：经具名 ActivitySource 创建或有父级）时以它为父级、取它的采样位；否则用调用的父跨度与标志，上游「不采样」原样传下去，没有上游时按已采样传下去。消息信封与插件调用的元数据共用这一取法（ADR-0235）
    /// </remarks>
    public static TraceParent OutgoingTraceParent(this InvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // 不经 ActivitySource 直接构造的根 Activity 的标志只是缺省值，不代表「不采样」，所以要求有具名来源或父级
        if (Activity.Current is { IdFormat: ActivityIdFormat.W3C } current
            && string.Equals(current.TraceId.ToHexString(), context.TraceId, StringComparison.Ordinal)
            && (current.Source.Name.Length > 0 || current.ParentSpanId != default))
        {
            return new TraceParent(context.TraceId, current.SpanId.ToHexString(), current.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded));
        }

        return new TraceParent(context.TraceId, context.SpanId, context.TraceFlags is not { } flags || flags.HasFlag(ActivityTraceFlags.Recorded));
    }

    /// <summary>
    /// 读取强类型项
    /// </summary>
    public static Maybe<T> Find<T>(this InvocationContext context, ContextItemKey<T> key)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(key);
        // 键存在但值类型不符也视为无值，不抛转换异常
        return context.Items.TryGetValue(key, out var value) && value is T typed ? Maybe.Some(typed) : default;
    }

    /// <summary>
    /// 写入强类型项
    /// </summary>
    public static void Set<T>(this InvocationContext context, ContextItemKey<T> key, T value)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        context.Items[key] = value;
    }

    /// <summary>
    /// 读取本次调用的结果
    /// </summary>
    /// <remarks>
    /// 尚未设置时为无值
    /// </remarks>
    public static Maybe<Result> FindOutcome(this InvocationContext context) => context.Find(_outcomeKey);

    /// <summary>
    /// 设置本次调用的结果
    /// </summary>
    /// <remarks>
    /// Egress 阶段据此翻译响应
    /// </remarks>
    public static void SetOutcome(this InvocationContext context, Result outcome) => context.Set(_outcomeKey, outcome);

    /// <summary>
    /// 读取调用来源
    /// </summary>
    /// <remarks>
    /// 内部发起、没有经协议适配器的调用为无值。派生上下文共享同一个来源
    /// </remarks>
    public static Maybe<InvocationOrigin> FindOrigin(this InvocationContext context) => context.Find(_originKey);

    /// <summary>
    /// 写入调用来源
    /// </summary>
    /// <remarks>
    /// 协议适配器建上下文时调用；之后的阶段只读
    /// </remarks>
    public static void SetOrigin(this InvocationContext context, InvocationOrigin origin) => context.Set(_originKey, origin);

    /// <summary>
    /// 若尚未设置结果，则设置为 <paramref name="outcome"/>
    /// </summary>
    /// <remarks>
    /// 已设置时保留原值
    /// </remarks>
    public static void SetOutcomeIfMissing(this InvocationContext context, Result outcome)
    {
        if (!context.FindOutcome().HasValue)
        {
            context.SetOutcome(outcome);
        }
    }
}
