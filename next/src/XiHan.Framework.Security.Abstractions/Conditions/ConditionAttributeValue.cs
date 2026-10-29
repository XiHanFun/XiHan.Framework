// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Conditions;

/// <summary>
/// 属性取值。
/// </summary>
/// <remarks>
/// 元素是原始 CLR 值（字符串、数值、布尔、日期时间、<see cref="System.Net.IPAddress"/> 等），由求值器按登记的值种类转换；<see langword="null"/> 元素会被丢掉。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class ConditionAttributeValue
{
    /// <summary>
    /// 空元素列表。
    /// </summary>
    private static readonly IReadOnlyList<object> _noValues = [];

    /// <summary>
    /// 创建。
    /// </summary>
    private ConditionAttributeValue(ConditionAttributeValueState state, IReadOnlyList<object> values)
    {
        State = state;
        Values = values;
    }

    /// <summary>
    /// 取不到。
    /// </summary>
    public static ConditionAttributeValue Missing { get; } = new(ConditionAttributeValueState.Missing, _noValues);

    /// <summary>
    /// 暂不可得。
    /// </summary>
    public static ConditionAttributeValue Deferred { get; } = new(ConditionAttributeValueState.Deferred, _noValues);

    /// <summary>
    /// 取值状态。
    /// </summary>
    public ConditionAttributeValueState State { get; }

    /// <summary>
    /// 值元素（单值属性至多一个）。
    /// </summary>
    public IReadOnlyList<object> Values { get; }

    /// <summary>
    /// 有值。
    /// </summary>
    /// <remarks>
    /// 非字符串的可枚举对象展开成多个元素；<paramref name="value"/> 为 <see langword="null"/> 时等同 <see cref="Missing"/>。
    /// </remarks>
    public static ConditionAttributeValue Of(object? value) => value switch
    {
        null => Missing,
        string text => new ConditionAttributeValue(ConditionAttributeValueState.Present, [text]),
        IEnumerable items => new ConditionAttributeValue(ConditionAttributeValueState.Present, [.. items.Cast<object?>().Where(static item => item is not null).Select(static item => item!)]),
        _ => new ConditionAttributeValue(ConditionAttributeValueState.Present, [value]),
    };
}
