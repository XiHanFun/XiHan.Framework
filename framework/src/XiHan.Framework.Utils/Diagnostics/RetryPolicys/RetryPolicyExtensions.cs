#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RetryPolicyExtensions
// Guid:3c46e1d7-077a-4775-9eff-298340653ec3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/05 07:28:44
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Diagnostics.RetryPolicys;

/// <summary>
/// 重试策略扩展方法
/// </summary>
public static class RetryPolicyExtensions
{
    /// <summary>
    /// 为重试策略添加随机抖动
    /// </summary>
    /// <param name="strategy">基础策略</param>
    /// <param name="jitterPercentage">抖动百分比</param>
    /// <returns>带抖动的策略</returns>
    public static IRetryStrategy WithJitter(this IRetryStrategy strategy, double jitterPercentage = 0.1)
    {
        return new JitteredStrategy(strategy, jitterPercentage);
    }

    /// <summary>
    /// 创建自定义重试条件
    /// </summary>
    /// <param name="condition">条件函数</param>
    /// <returns>重试条件</returns>
    public static IRetryCondition Where(Func<Exception, int, bool> condition)
    {
        return new CustomRetryCondition(condition);
    }

    /// <summary>
    /// 组合重试条件（AND）
    /// </summary>
    /// <param name="condition1">条件1</param>
    /// <param name="condition2">条件2</param>
    /// <returns>组合条件</returns>
    public static IRetryCondition And(this IRetryCondition condition1, IRetryCondition condition2)
    {
        return new AndRetryCondition(condition1, condition2);
    }

    /// <summary>
    /// 组合重试条件（OR）
    /// </summary>
    /// <param name="condition1">条件1</param>
    /// <param name="condition2">条件2</param>
    /// <returns>组合条件</returns>
    public static IRetryCondition Or(this IRetryCondition condition1, IRetryCondition condition2)
    {
        return new OrRetryCondition(condition1, condition2);
    }
}
