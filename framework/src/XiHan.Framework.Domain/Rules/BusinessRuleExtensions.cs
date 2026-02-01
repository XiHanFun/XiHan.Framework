#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BusinessRuleExtensions
// Guid:bce12f9d-8e3a-4b5c-9a2e-1234567890ef
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/12 18:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Exceptions;

namespace XiHan.Framework.Domain.Rules;

/// <summary>
/// 业务规则扩展方法
/// 提供便捷的业务规则验证功能
/// </summary>
public static class BusinessRuleExtensions
{
    /// <summary>
    /// 检查业务规则，如果规则被违反则抛出异常
    /// </summary>
    /// <param name="rule">业务规则</param>
    /// <exception cref="ArgumentNullException">当规则为空时抛出</exception>
    /// <exception cref="BusinessRuleValidationException">当业务规则被违反时抛出</exception>
    public static void CheckRule(this IBusinessRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (rule.IsBroken())
        {
            throw new BusinessRuleValidationException(rule);
        }
    }

    /// <summary>
    /// 批量检查业务规则
    /// </summary>
    /// <param name="rules">业务规则集合</param>
    /// <exception cref="ArgumentNullException">当规则集合为空时抛出</exception>
    /// <exception cref="BusinessRuleValidationException">当任何业务规则被违反时抛出</exception>
    public static void CheckRules(this IEnumerable<IBusinessRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        var brokenRules = rules.Where(rule => rule.IsBroken()).ToList();

        if (brokenRules.Count != 0)
        {
            var messages = brokenRules.Select(rule => rule.Message);
            var combinedMessage = string.Join("; ", messages);
            throw new BusinessRuleValidationException(combinedMessage);
        }
    }

    /// <summary>
    /// 异步检查业务规则
    /// </summary>
    /// <param name="rule">业务规则</param>
    /// <returns>任务</returns>
    /// <exception cref="ArgumentNullException">当规则为空时抛出</exception>
    /// <exception cref="BusinessRuleValidationException">当业务规则被违反时抛出</exception>
    public static Task CheckRuleAsync(this IBusinessRule rule)
    {
        return Task.Run(rule.CheckRule);
    }

    /// <summary>
    /// 异步批量检查业务规则
    /// </summary>
    /// <param name="rules">业务规则集合</param>
    /// <returns>任务</returns>
    /// <exception cref="ArgumentNullException">当规则集合为空时抛出</exception>
    /// <exception cref="BusinessRuleValidationException">当任何业务规则被违反时抛出</exception>
    public static Task CheckRulesAsync(this IEnumerable<IBusinessRule> rules)
    {
        return Task.Run(rules.CheckRules);
    }

    /// <summary>
    /// 验证业务规则并返回验证结果
    /// </summary>
    /// <param name="rule">业务规则</param>
    /// <returns>验证结果</returns>
    /// <exception cref="ArgumentNullException">当规则为空时抛出</exception>
    public static BusinessRuleValidationResult Validate(this IBusinessRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule.IsBroken()
            ? BusinessRuleValidationResult.Failure(rule.Message)
            : BusinessRuleValidationResult.Success();
    }

    /// <summary>
    /// 批量验证业务规则并返回验证结果
    /// </summary>
    /// <param name="rules">业务规则集合</param>
    /// <returns>验证结果</returns>
    /// <exception cref="ArgumentNullException">当规则集合为空时抛出</exception>
    public static BusinessRuleValidationResult ValidateAll(this IEnumerable<IBusinessRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        var brokenRules = rules.Where(rule => rule.IsBroken()).ToList();

        return brokenRules.Count == 0
            ? BusinessRuleValidationResult.Success()
            : BusinessRuleValidationResult.Failure(brokenRules.Select(rule => rule.Message));
    }
}

/// <summary>
/// 业务规则验证结果
/// </summary>
public class BusinessRuleValidationResult
{
    private BusinessRuleValidationResult(bool isValid, IEnumerable<string>? errors = null)
    {
        IsValid = isValid;
        Errors = errors?.ToList() ?? [];
    }

    /// <summary>
    /// 是否验证成功
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// 验证错误信息
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// 创建成功的验证结果
    /// </summary>
    /// <returns>成功的验证结果</returns>
    public static BusinessRuleValidationResult Success()
    {
        return new BusinessRuleValidationResult(true);
    }

    /// <summary>
    /// 创建失败的验证结果
    /// </summary>
    /// <param name="error">错误信息</param>
    /// <returns>失败的验证结果</returns>
    public static BusinessRuleValidationResult Failure(string error)
    {
        return new BusinessRuleValidationResult(false, [error]);
    }

    /// <summary>
    /// 创建失败的验证结果
    /// </summary>
    /// <param name="errors">错误信息集合</param>
    /// <returns>失败的验证结果</returns>
    public static BusinessRuleValidationResult Failure(IEnumerable<string> errors)
    {
        return new BusinessRuleValidationResult(false, errors);
    }

    /// <summary>
    /// 重写 ToString 方法
    /// </summary>
    /// <returns>字符串表示</returns>
    public override string ToString()
    {
        return IsValid
            ? "Validation successful"
            : $"Validation failed: {string.Join("; ", Errors)}";
    }
}
