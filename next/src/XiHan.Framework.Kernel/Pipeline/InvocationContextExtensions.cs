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
    private static readonly ContextItemKey<Result> _outcomeKey = new("xihan.outcome");

    /// <summary>
    /// 读取强类型项。
    /// </summary>
    public static Maybe<T> Get<T>(this InvocationContext context, ContextItemKey<T> key)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(key);
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
    /// 读取本次调用的结果；尚未设置时为无值。
    /// </summary>
    public static Maybe<Result> GetOutcome(this InvocationContext context) => context.Get(_outcomeKey);

    /// <summary>
    /// 设置本次调用的结果。Egress 阶段据此翻译响应。
    /// </summary>
    public static void SetOutcome(this InvocationContext context, Result outcome) => context.Set(_outcomeKey, outcome);

    /// <summary>
    /// 若尚未设置结果，则设置为 <paramref name="outcome"/>；已设置时保留原值。
    /// </summary>
    public static void SetOutcomeIfMissing(this InvocationContext context, Result outcome)
    {
        if (!context.GetOutcome().HasValue)
        {
            context.SetOutcome(outcome);
        }
    }
}
