// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Conditions;

/// <summary>
/// 运算符与属性值种类的搭配规则。
/// </summary>
/// <remarks>
/// 编译器据此拒绝不搭配的子句，条件编辑界面据此只列出能用的运算符。等于 / 不等于 / 在集合中 / 不在集合中 / 为空 / 不为空对任何属性可用；比大小与区间只用于数字、日期时间、日期、时刻；包含 / 不包含用于多值属性与字符串；开头 / 结尾只用于字符串。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class ConditionOperatorRules
{
    /// <summary>
    /// 运算符能否用于该属性。
    /// </summary>
    public static bool IsAllowed(ConditionOperator @operator, ConditionAttributeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return @operator switch
        {
            ConditionOperator.Equals or ConditionOperator.NotEquals or ConditionOperator.In or ConditionOperator.NotIn or ConditionOperator.IsNull or ConditionOperator.IsNotNull => true,
            ConditionOperator.GreaterThan or ConditionOperator.GreaterThanOrEquals or ConditionOperator.LessThan or ConditionOperator.LessThanOrEquals or ConditionOperator.Between => ConditionValues.IsOrdered(descriptor.Kind),
            ConditionOperator.Contains or ConditionOperator.NotContains => descriptor.IsMultiValued || descriptor.Kind == ConditionValueKind.String,
            ConditionOperator.StartsWith or ConditionOperator.EndsWith => descriptor.Kind == ConditionValueKind.String,
            _ => false,
        };
    }

    /// <summary>
    /// 该属性能用的全部运算符，按枚举值排序。
    /// </summary>
    public static IReadOnlyList<ConditionOperator> AllowedFor(ConditionAttributeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return [.. Enum.GetValues<ConditionOperator>().Where(@operator => IsAllowed(@operator, descriptor))];
    }
}
