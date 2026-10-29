// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Pipeline;

/// <summary>
/// 调用上下文的强类型 Items 访问与调用结果槽。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class InvocationContextExtensions
{
    /// <summary>
    /// 调用结果在 <see cref="InvocationContext.Items"/> 中的键。
    /// </summary>
    private static readonly ContextItemKey<Result> _outcomeKey = new("xihan.outcome");

    /// <summary>
    /// 调用来源在 <see cref="InvocationContext.Items"/> 中的键。
    /// </summary>
    private static readonly ContextItemKey<InvocationOrigin> _originKey = new("xihan.origin");

    /// <summary>
    /// 读取强类型项。
    /// </summary>
    public static Maybe<T> Get<T>(this InvocationContext context, ContextItemKey<T> key)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(key);
        // 键存在但值类型不符也视为无值，不抛转换异常。
        return context.Items.TryGetValue(key, out var value) && value is T typed ? Maybe.Some(typed) : default;
    }

    /// <summary>
    /// 写入强类型项。
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
    /// 读取本次调用的结果。
    /// </summary>
    /// <remarks>
    /// 尚未设置时为无值。
    /// </remarks>
    public static Maybe<Result> GetOutcome(this InvocationContext context) => context.Get(_outcomeKey);

    /// <summary>
    /// 设置本次调用的结果。
    /// </summary>
    /// <remarks>
    /// Egress 阶段据此翻译响应。
    /// </remarks>
    public static void SetOutcome(this InvocationContext context, Result outcome) => context.Set(_outcomeKey, outcome);

    /// <summary>
    /// 读取调用来源。
    /// </summary>
    /// <remarks>
    /// 内部发起、没有经协议适配器的调用为无值。派生上下文共享同一个来源。
    /// </remarks>
    public static Maybe<InvocationOrigin> GetOrigin(this InvocationContext context) => context.Get(_originKey);

    /// <summary>
    /// 写入调用来源。
    /// </summary>
    /// <remarks>
    /// 协议适配器建上下文时调用；之后的阶段只读。
    /// </remarks>
    public static void SetOrigin(this InvocationContext context, InvocationOrigin origin) => context.Set(_originKey, origin);

    /// <summary>
    /// 若尚未设置结果，则设置为 <paramref name="outcome"/>。
    /// </summary>
    /// <remarks>
    /// 已设置时保留原值。
    /// </remarks>
    public static void SetOutcomeIfMissing(this InvocationContext context, Result outcome)
    {
        if (!context.GetOutcome().HasValue)
        {
            context.SetOutcome(outcome);
        }
    }
}
