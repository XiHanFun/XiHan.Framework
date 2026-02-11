#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EntityExtensions
// Guid:5be86ad3-95ab-4c78-abab-fc77cccc5135
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/12 16:20:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Exceptions;
using XiHan.Framework.Domain.Rules;

namespace XiHan.Framework.Domain.Extensions;

/// <summary>
/// 实体扩展方法
/// </summary>
public static class EntityExtensions
{
    /// <summary>
    /// 检查业务规则
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="rule">业务规则</param>
    /// <exception cref="BusinessRuleValidationException">当业务规则违反时抛出</exception>
    public static void CheckRule(this IEntityBase entity, IBusinessRule rule)
    {
        if (rule.IsBroken())
        {
            throw new BusinessRuleValidationException(rule);
        }
    }

    /// <summary>
    /// 检查多个业务规则
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="rules">业务规则集合</param>
    /// <exception cref="BusinessRuleValidationException">当任一业务规则违反时抛出</exception>
    public static void CheckRules(this IEntityBase entity, params IBusinessRule[] rules)
    {
        foreach (var rule in rules)
        {
            entity.CheckRule(rule);
        }
    }

    /// <summary>
    /// 检查多个业务规则
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="rules">业务规则集合</param>
    /// <exception cref="BusinessRuleValidationException">当任一业务规则违反时抛出</exception>
    public static void CheckRules(this IEntityBase entity, IEnumerable<IBusinessRule> rules)
    {
        foreach (var rule in rules)
        {
            entity.CheckRule(rule);
        }
    }

    /// <summary>
    /// 尝试检查业务规则
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="rule">业务规则</param>
    /// <returns>如果规则通过返回 true，否则返回 false</returns>
    public static bool TryCheckRule(this IEntityBase entity, IBusinessRule rule)
    {
        return !rule.IsBroken();
    }

    /// <summary>
    /// 验证所有业务规则
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="rules">业务规则集合</param>
    /// <returns>违反的业务规则集合</returns>
    public static IEnumerable<IBusinessRule> ValidateRules(this IEntityBase entity, params IBusinessRule[] rules)
    {
        return rules.Where(rule => rule.IsBroken());
    }

    /// <summary>
    /// 验证所有业务规则
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="rules">业务规则集合</param>
    /// <returns>违反的业务规则集合</returns>
    public static IEnumerable<IBusinessRule> ValidateRules(this IEntityBase entity, IEnumerable<IBusinessRule> rules)
    {
        return rules.Where(rule => rule.IsBroken());
    }
}
