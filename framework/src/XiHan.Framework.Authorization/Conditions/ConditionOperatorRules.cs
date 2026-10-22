// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization.Conditions;

/// <summary>
/// 运算符与属性值种类的搭配规则
/// </summary>
/// <remarks>
/// 编译器据此拒绝不搭配的子句；条件编辑界面据此只列出能用的运算符。
/// </remarks>
public static class ConditionOperatorRules
{
    /// <summary>
    /// 运算符能否用于该属性
    /// </summary>
    /// <param name="operator">运算符</param>
    /// <param name="descriptor">属性登记项</param>
    /// <returns>能用返回 <see langword="true"/></returns>
    public static bool IsAllowed(ConditionOperator @operator, ConditionAttributeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return @operator switch
        {
            ConditionOperator.Equals or ConditionOperator.NotEquals
                or ConditionOperator.In or ConditionOperator.NotIn
                or ConditionOperator.IsNull or ConditionOperator.IsNotNull => true,
            ConditionOperator.GreaterThan or ConditionOperator.GreaterThanOrEquals
                or ConditionOperator.LessThan or ConditionOperator.LessThanOrEquals
                or ConditionOperator.Between => ConditionValues.IsOrdered(descriptor.Kind),
            ConditionOperator.Contains or ConditionOperator.NotContains => descriptor.IsMultiValued || descriptor.Kind == ConditionValueKind.String,
            ConditionOperator.StartsWith or ConditionOperator.EndsWith => descriptor.Kind == ConditionValueKind.String,
            _ => false
        };
    }

    /// <summary>
    /// 该属性能用的全部运算符，按枚举值排序
    /// </summary>
    /// <param name="descriptor">属性登记项</param>
    /// <returns>运算符列表</returns>
    public static IReadOnlyList<ConditionOperator> AllowedFor(ConditionAttributeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return [.. Enum.GetValues<ConditionOperator>().Where(@operator => IsAllowed(@operator, descriptor))];
    }
}
