#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RetryPolicyFactory
// Guid:dfd75f51-ca2a-4f36-9b67-1087daec8476
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/5 7:34:13
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Diagnostics.RetryPolicys;

/// <summary>
/// 重试策略工厂
/// </summary>
public class RetryPolicyFactory
{
    /// <summary>
    /// 创建具有固定延迟的重试策略
    /// </summary>
    /// <param name="maxRetries">最大重试次数</param>
    /// <param name="delay">固定延迟时间</param>
    /// <returns>重试策略实例</returns>
    public static RetryPolicy WithFixedDelay(int maxRetries, TimeSpan delay)
    {
        return new RetryPolicy(maxRetries, new FixedDelayStrategy(delay));
    }

    /// <summary>
    /// 创建具有指数退避的重试策略
    /// </summary>
    /// <param name="maxRetries">最大重试次数</param>
    /// <param name="baseDelay">基础延迟时间</param>
    /// <param name="backoffMultiplier">退避倍数</param>
    /// <param name="maxDelay">最大延迟时间</param>
    /// <returns>重试策略实例</returns>
    public static RetryPolicy WithExponentialBackoff(int maxRetries, TimeSpan baseDelay, double backoffMultiplier = 2.0, TimeSpan? maxDelay = null)
    {
        return new RetryPolicy(maxRetries, new ExponentialBackoffStrategy(baseDelay, backoffMultiplier, maxDelay));
    }

    /// <summary>
    /// 创建具有线性增长延迟的重试策略
    /// </summary>
    /// <param name="maxRetries">最大重试次数</param>
    /// <param name="baseDelay">基础延迟时间</param>
    /// <param name="increment">每次递增的延迟时间</param>
    /// <returns>重试策略实例</returns>
    public static RetryPolicy WithLinearBackoff(int maxRetries, TimeSpan baseDelay, TimeSpan increment)
    {
        return new RetryPolicy(maxRetries, new LinearBackoffStrategy(baseDelay, increment));
    }

    /// <summary>
    /// 创建立即重试的策略（无延迟）
    /// </summary>
    /// <param name="maxRetries">最大重试次数</param>
    /// <returns>重试策略实例</returns>
    public static RetryPolicy WithImmediateRetry(int maxRetries)
    {
        return new RetryPolicy(maxRetries, new ImmediateStrategy());
    }

    /// <summary>
    /// 创建针对特定异常类型的重试策略
    /// </summary>
    /// <typeparam name="TException">异常类型</typeparam>
    /// <param name="maxRetries">最大重试次数</param>
    /// <param name="strategy">重试策略</param>
    /// <returns>重试策略实例</returns>
    public static RetryPolicy ForException<TException>(int maxRetries, IRetryStrategy? strategy = null)
        where TException : Exception
    {
        return new RetryPolicy(maxRetries, strategy, new ExceptionTypeRetryCondition<TException>());
    }
}
