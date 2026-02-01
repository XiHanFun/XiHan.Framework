#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RetryStrategy
// Guid:d916564c-15e8-410a-bf5a-93fbb088984a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/05 07:32:43
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Diagnostics.RetryPolicys;

/// <summary>
/// 固定延迟策略
/// </summary>
public class FixedDelayStrategy : IRetryStrategy
{
    private readonly TimeSpan _delay;

    /// <summary>
    /// 初始化固定延迟策略
    /// </summary>
    /// <param name="delay">固定延迟时间</param>
    public FixedDelayStrategy(TimeSpan delay)
    {
        _delay = delay;
    }

    /// <summary>
    /// 获取延迟时间
    /// </summary>
    /// <param name="retryCount">重试次数</param>
    /// <returns>固定的延迟时间</returns>
    public TimeSpan GetDelay(int retryCount)
    {
        return _delay;
    }
}

/// <summary>
/// 指数退避策略
/// </summary>
public class ExponentialBackoffStrategy : IRetryStrategy
{
    private readonly TimeSpan _baseDelay;
    private readonly double _backoffMultiplier;
    private readonly TimeSpan? _maxDelay;

    /// <summary>
    /// 初始化指数退避策略
    /// </summary>
    /// <param name="baseDelay">基础延迟时间</param>
    /// <param name="backoffMultiplier">退避倍数</param>
    /// <param name="maxDelay">最大延迟时间</param>
    public ExponentialBackoffStrategy(TimeSpan baseDelay, double backoffMultiplier = 2.0, TimeSpan? maxDelay = null)
    {
        _baseDelay = baseDelay;
        _backoffMultiplier = backoffMultiplier;
        _maxDelay = maxDelay;
    }

    /// <summary>
    /// 获取延迟时间
    /// </summary>
    /// <param name="retryCount">重试次数</param>
    /// <returns>指数增长的延迟时间</returns>
    public TimeSpan GetDelay(int retryCount)
    {
        var delay = TimeSpan.FromTicks((long)(_baseDelay.Ticks * Math.Pow(_backoffMultiplier, retryCount - 1)));

        if (_maxDelay.HasValue && delay > _maxDelay.Value)
        {
            delay = _maxDelay.Value;
        }

        return delay;
    }
}

/// <summary>
/// 线性退避策略
/// </summary>
public class LinearBackoffStrategy : IRetryStrategy
{
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _increment;

    /// <summary>
    /// 初始化线性退避策略
    /// </summary>
    /// <param name="baseDelay">基础延迟时间</param>
    /// <param name="increment">每次递增的延迟时间</param>
    public LinearBackoffStrategy(TimeSpan baseDelay, TimeSpan increment)
    {
        _baseDelay = baseDelay;
        _increment = increment;
    }

    /// <summary>
    /// 获取延迟时间
    /// </summary>
    /// <param name="retryCount">重试次数</param>
    /// <returns>线性增长的延迟时间</returns>
    public TimeSpan GetDelay(int retryCount)
    {
        return _baseDelay + TimeSpan.FromTicks(_increment.Ticks * (retryCount - 1));
    }
}

/// <summary>
/// 立即重试策略（无延迟）
/// </summary>
public class ImmediateStrategy : IRetryStrategy
{
    /// <summary>
    /// 获取延迟时间
    /// </summary>
    /// <param name="retryCount">重试次数</param>
    /// <returns>零延迟</returns>
    public TimeSpan GetDelay(int retryCount)
    {
        return TimeSpan.Zero;
    }
}

/// <summary>
/// 随机抖动策略
/// </summary>
public class JitteredStrategy : IRetryStrategy
{
    private readonly IRetryStrategy _baseStrategy;
    private readonly double _jitterPercentage;
    private readonly Random _random = new();

    /// <summary>
    /// 初始化随机抖动策略
    /// </summary>
    /// <param name="baseStrategy">基础策略</param>
    /// <param name="jitterPercentage">抖动百分比（0-1）</param>
    public JitteredStrategy(IRetryStrategy baseStrategy, double jitterPercentage = 0.1)
    {
        _baseStrategy = baseStrategy;
        _jitterPercentage = Math.Clamp(jitterPercentage, 0, 1);
    }

    /// <summary>
    /// 获取加入随机抖动的延迟时间
    /// </summary>
    /// <param name="retryCount">重试次数</param>
    /// <returns>带抖动的延迟时间</returns>
    public TimeSpan GetDelay(int retryCount)
    {
        var baseDelay = _baseStrategy.GetDelay(retryCount);
        var jitter = (_random.NextDouble() * _jitterPercentage * 2) - _jitterPercentage; // -jitterPercentage 到 +jitterPercentage
        var adjustedTicks = (long)(baseDelay.Ticks * (1 + jitter));
        return TimeSpan.FromTicks(Math.Max(0, adjustedTicks));
    }
}
