// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections;

namespace XiHan.Framework.Authorization.Conditions;

/// <summary>
/// 属性取值
/// </summary>
/// <remarks>
/// 元素是原始 CLR 值（字符串、数值、布尔、日期时间、<see cref="System.Net.IPAddress"/> 等），由求值器按登记的值种类转换；
/// <see langword="null"/> 元素会被丢掉。
/// </remarks>
public sealed class ConditionAttributeValue
{
    private static readonly IReadOnlyList<object> NoValues = [];

    private ConditionAttributeValue(ConditionAttributeValueState state, IReadOnlyList<object> values)
    {
        State = state;
        Values = values;
    }

    /// <summary>
    /// 取不到
    /// </summary>
    public static ConditionAttributeValue Missing { get; } = new(ConditionAttributeValueState.Missing, NoValues);

    /// <summary>
    /// 暂不可得
    /// </summary>
    public static ConditionAttributeValue Deferred { get; } = new(ConditionAttributeValueState.Deferred, NoValues);

    /// <summary>
    /// 取值状态
    /// </summary>
    public ConditionAttributeValueState State { get; }

    /// <summary>
    /// 值元素（单值属性至多一个）
    /// </summary>
    public IReadOnlyList<object> Values { get; }

    /// <summary>
    /// 有值：非字符串的可枚举对象展开成多个元素
    /// </summary>
    /// <param name="value">值；为 <see langword="null"/> 时等同 <see cref="Missing"/></param>
    /// <returns>取值</returns>
    public static ConditionAttributeValue Of(object? value)
    {
        return value switch
        {
            null => Missing,
            string text => new ConditionAttributeValue(ConditionAttributeValueState.Present, [text]),
            IEnumerable items => new ConditionAttributeValue(
                ConditionAttributeValueState.Present,
                [.. items.Cast<object?>().Where(static item => item is not null).Select(static item => item!)]),
            _ => new ConditionAttributeValue(ConditionAttributeValueState.Present, [value])
        };
    }
}
