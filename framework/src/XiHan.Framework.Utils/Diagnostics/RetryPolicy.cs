#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RetryPolicy
// Guid:a1b2c3d4-e5f6-7890-1234-567890abcdef
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/19 14:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics;

namespace XiHan.Framework.Utils.Diagnostics;

/// <summary>
/// 重试策略类
/// </summary>
/// <remarks>
/// 提供灵活的重试机制，支持多种重试策略、条件重试、异步操作等功能
/// </remarks>
public class RetryPolicy
{
    #region 私有字段

    private readonly int _maxRetries;
    private readonly IRetryStrategy _retryStrategy;
    private readonly IRetryCondition _retryCondition;
    private readonly TimeSpan? _timeout;
    private readonly Action<RetryAttempt>? _onRetryCallback;
    private readonly Action<RetryResult>? _onFailureCallback;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化重试策略
    /// </summary>
    /// <param name="maxRetries">最大重试次数</param>
    /// <param name="retryStrategy">重试策略</param>
    /// <param name="retryCondition">重试条件</param>
    /// <param name="timeout">总超时时间</param>
    /// <param name="onRetryCallback">重试时的回调</param>
    /// <param name="onFailureCallback">失败时的回调</param>
    public RetryPolicy(
        int maxRetries = 3,
        IRetryStrategy? retryStrategy = null,
        IRetryCondition? retryCondition = null,
        TimeSpan? timeout = null,
        Action<RetryAttempt>? onRetryCallback = null,
        Action<RetryResult>? onFailureCallback = null)
    {
        Guard.Range(maxRetries, nameof(maxRetries), 0, 100);
        
        _maxRetries = maxRetries;
        _retryStrategy = retryStrategy ?? new FixedDelayStrategy(TimeSpan.FromSeconds(1));
        _retryCondition = retryCondition ?? new DefaultRetryCondition();
        _timeout = timeout;
        _onRetryCallback = onRetryCallback;
        _onFailureCallback = onFailureCallback;
    }

    #endregion

    #region 公开方法 - 同步执行

    /// <summary>
    /// 执行操作并应用重试策略
    /// </summary>
    /// <param name="action">要执行的操作</param>
    /// <returns>重试结果</returns>
    public RetryResult Execute(Action action)
    {
        return Execute(() =>
        {
            action();
            return true;
        });
    }

    /// <summary>
    /// 执行函数并应用重试策略
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="func">要执行的函数</param>
    /// <returns>重试结果</returns>
    public RetryResult<T> Execute<T>(Func<T> func)
    {
        Guard.NotNull(func, nameof(func));

        var stopwatch = Stopwatch.StartNew();
        var attempts = new List<RetryAttempt>();
        Exception? lastException = null;
        T? result = default;
        var success = false;

        try
        {
            for (var attempt = 0; attempt <= _maxRetries; attempt++)
            {
                // 检查超时
                if (_timeout.HasValue && stopwatch.Elapsed > _timeout.Value)
                {
                    break;
                }

                var attemptStopwatch = Stopwatch.StartNew();
                try
                {
                    result = func();
                    success = true;
                    attemptStopwatch.Stop();

                    var retryAttempt = new RetryAttempt
                    {
                        AttemptNumber = attempt + 1,
                        IsSuccess = true,
                        ElapsedTime = attemptStopwatch.Elapsed,
                        Exception = null
                    };
                    attempts.Add(retryAttempt);

                    break;
                }
                catch (Exception ex)
                {
                    attemptStopwatch.Stop();
                    lastException = ex;

                    var retryAttempt = new RetryAttempt
                    {
                        AttemptNumber = attempt + 1,
                        IsSuccess = false,
                        ElapsedTime = attemptStopwatch.Elapsed,
                        Exception = ex
                    };
                    attempts.Add(retryAttempt);

                    // 检查是否应该重试
                    if (attempt < _maxRetries && _retryCondition.ShouldRetry(ex, attempt + 1))
                    {
                        _onRetryCallback?.Invoke(retryAttempt);

                        // 计算延迟时间
                        var delay = _retryStrategy.GetDelay(attempt + 1);
                        if (delay > TimeSpan.Zero)
                        {
                            // 检查延迟后是否会超时
                            if (_timeout.HasValue && stopwatch.Elapsed + delay > _timeout.Value)
                            {
                                break;
                            }

                            Thread.Sleep(delay);
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
        finally
        {
            stopwatch.Stop();
        }

        var retryResult = new RetryResult<T>
        {
            IsSuccess = success,
            Result = result,
            Exception = lastException,
            TotalElapsedTime = stopwatch.Elapsed,
            Attempts = attempts,
            TotalAttempts = attempts.Count
        };

        if (!success)
        {
            _onFailureCallback?.Invoke(retryResult);
        }

        return retryResult;
    }

    #endregion

    #region 公开方法 - 异步执行

    /// <summary>
    /// 异步执行操作并应用重试策略
    /// </summary>
    /// <param name="action">要执行的异步操作</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>重试结果</returns>
    public async Task<RetryResult> ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(async () =>
        {
            await action();
            return true;
        }, cancellationToken);

        return new RetryResult
        {
            IsSuccess = result.IsSuccess,
            Exception = result.Exception,
            TotalElapsedTime = result.TotalElapsedTime,
            Attempts = result.Attempts,
            TotalAttempts = result.TotalAttempts
        };
    }

    /// <summary>
    /// 异步执行函数并应用重试策略
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="func">要执行的异步函数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>重试结果</returns>
    public async Task<RetryResult<T>> ExecuteAsync<T>(Func<Task<T>> func, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(func, nameof(func));

        var stopwatch = Stopwatch.StartNew();
        var attempts = new List<RetryAttempt>();
        Exception? lastException = null;
        T? result = default;
        var success = false;

        try
        {
            for (var attempt = 0; attempt <= _maxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 检查超时
                if (_timeout.HasValue && stopwatch.Elapsed > _timeout.Value)
                {
                    break;
                }

                var attemptStopwatch = Stopwatch.StartNew();
                try
                {
                    result = await func();
                    success = true;
                    attemptStopwatch.Stop();

                    var retryAttempt = new RetryAttempt
                    {
                        AttemptNumber = attempt + 1,
                        IsSuccess = true,
                        ElapsedTime = attemptStopwatch.Elapsed,
                        Exception = null
                    };
                    attempts.Add(retryAttempt);

                    break;
                }
                catch (Exception ex)
                {
                    attemptStopwatch.Stop();
                    lastException = ex;

                    var retryAttempt = new RetryAttempt
                    {
                        AttemptNumber = attempt + 1,
                        IsSuccess = false,
                        ElapsedTime = attemptStopwatch.Elapsed,
                        Exception = ex
                    };
                    attempts.Add(retryAttempt);

                    // 检查是否应该重试
                    if (attempt < _maxRetries && _retryCondition.ShouldRetry(ex, attempt + 1))
                    {
                        _onRetryCallback?.Invoke(retryAttempt);

                        // 计算延迟时间
                        var delay = _retryStrategy.GetDelay(attempt + 1);
                        if (delay > TimeSpan.Zero)
                        {
                            // 检查延迟后是否会超时
                            if (_timeout.HasValue && stopwatch.Elapsed + delay > _timeout.Value)
                            {
                                break;
                            }

                            await Task.Delay(delay, cancellationToken);
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
        finally
        {
            stopwatch.Stop();
        }

        var retryResult = new RetryResult<T>
        {
            IsSuccess = success,
            Result = result,
            Exception = lastException,
            TotalElapsedTime = stopwatch.Elapsed,
            Attempts = attempts,
            TotalAttempts = attempts.Count
        };

        if (!success)
        {
            _onFailureCallback?.Invoke(retryResult);
        }

        return retryResult;
    }

    #endregion

    #region 静态工厂方法

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

    #endregion
}

#region 重试策略接口和实现

/// <summary>
/// 重试策略接口
/// </summary>
public interface IRetryStrategy
{
    /// <summary>
    /// 获取指定重试次数的延迟时间
    /// </summary>
    /// <param name="retryCount">重试次数</param>
    /// <returns>延迟时间</returns>
    TimeSpan GetDelay(int retryCount);
}

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
        var jitter = _random.NextDouble() * _jitterPercentage * 2 - _jitterPercentage; // -jitterPercentage 到 +jitterPercentage
        var adjustedTicks = (long)(baseDelay.Ticks * (1 + jitter));
        return TimeSpan.FromTicks(Math.Max(0, adjustedTicks));
    }
}

#endregion

#region 重试条件接口和实现

/// <summary>
/// 重试条件接口
/// </summary>
public interface IRetryCondition
{
    /// <summary>
    /// 判断是否应该重试
    /// </summary>
    /// <param name="exception">发生的异常</param>
    /// <param name="attemptNumber">当前尝试次数</param>
    /// <returns>是否应该重试</returns>
    bool ShouldRetry(Exception exception, int attemptNumber);
}

/// <summary>
/// 默认重试条件（对所有异常都重试）
/// </summary>
public class DefaultRetryCondition : IRetryCondition
{
    /// <summary>
    /// 判断是否应该重试
    /// </summary>
    /// <param name="exception">发生的异常</param>
    /// <param name="attemptNumber">当前尝试次数</param>
    /// <returns>总是返回true</returns>
    public bool ShouldRetry(Exception exception, int attemptNumber)
    {
        return true;
    }
}

/// <summary>
/// 基于异常类型的重试条件
/// </summary>
/// <typeparam name="TException">要重试的异常类型</typeparam>
public class ExceptionTypeRetryCondition<TException> : IRetryCondition
    where TException : Exception
{
    /// <summary>
    /// 判断是否应该重试
    /// </summary>
    /// <param name="exception">发生的异常</param>
    /// <param name="attemptNumber">当前尝试次数</param>
    /// <returns>异常是指定类型时返回true</returns>
    public bool ShouldRetry(Exception exception, int attemptNumber)
    {
        return exception is TException;
    }
}

/// <summary>
/// 自定义重试条件
/// </summary>
public class CustomRetryCondition : IRetryCondition
{
    private readonly Func<Exception, int, bool> _condition;

    /// <summary>
    /// 初始化自定义重试条件
    /// </summary>
    /// <param name="condition">重试条件函数</param>
    public CustomRetryCondition(Func<Exception, int, bool> condition)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
    }

    /// <summary>
    /// 判断是否应该重试
    /// </summary>
    /// <param name="exception">发生的异常</param>
    /// <param name="attemptNumber">当前尝试次数</param>
    /// <returns>自定义条件的判断结果</returns>
    public bool ShouldRetry(Exception exception, int attemptNumber)
    {
        return _condition(exception, attemptNumber);
    }
}

/// <summary>
/// 组合重试条件（AND 逻辑）
/// </summary>
public class AndRetryCondition : IRetryCondition
{
    private readonly IRetryCondition[] _conditions;

    /// <summary>
    /// 初始化组合重试条件
    /// </summary>
    /// <param name="conditions">要组合的条件</param>
    public AndRetryCondition(params IRetryCondition[] conditions)
    {
        _conditions = conditions ?? throw new ArgumentNullException(nameof(conditions));
    }

    /// <summary>
    /// 判断是否应该重试
    /// </summary>
    /// <param name="exception">发生的异常</param>
    /// <param name="attemptNumber">当前尝试次数</param>
    /// <returns>所有条件都满足时返回true</returns>
    public bool ShouldRetry(Exception exception, int attemptNumber)
    {
        return _conditions.All(condition => condition.ShouldRetry(exception, attemptNumber));
    }
}

/// <summary>
/// 组合重试条件（OR 逻辑）
/// </summary>
public class OrRetryCondition : IRetryCondition
{
    private readonly IRetryCondition[] _conditions;

    /// <summary>
    /// 初始化组合重试条件
    /// </summary>
    /// <param name="conditions">要组合的条件</param>
    public OrRetryCondition(params IRetryCondition[] conditions)
    {
        _conditions = conditions ?? throw new ArgumentNullException(nameof(conditions));
    }

    /// <summary>
    /// 判断是否应该重试
    /// </summary>
    /// <param name="exception">发生的异常</param>
    /// <param name="attemptNumber">当前尝试次数</param>
    /// <returns>任一条件满足时返回true</returns>
    public bool ShouldRetry(Exception exception, int attemptNumber)
    {
        return _conditions.Any(condition => condition.ShouldRetry(exception, attemptNumber));
    }
}

#endregion

#region 重试结果类

/// <summary>
/// 重试结果基类
/// </summary>
public class RetryResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 最后一次的异常
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// 总耗时
    /// </summary>
    public TimeSpan TotalElapsedTime { get; set; }

    /// <summary>
    /// 所有尝试的记录
    /// </summary>
    public List<RetryAttempt> Attempts { get; set; } = [];

    /// <summary>
    /// 总尝试次数
    /// </summary>
    public int TotalAttempts { get; set; }

    /// <summary>
    /// 是否超时
    /// </summary>
    public bool IsTimeout => !IsSuccess && Attempts.Any();

    /// <summary>
    /// 平均每次尝试的耗时
    /// </summary>
    public TimeSpan AverageAttemptTime => TotalAttempts > 0 
        ? TimeSpan.FromTicks(TotalElapsedTime.Ticks / TotalAttempts) 
        : TimeSpan.Zero;
}

/// <summary>
/// 带返回值的重试结果
/// </summary>
/// <typeparam name="T">返回值类型</typeparam>
public class RetryResult<T> : RetryResult
{
    /// <summary>
    /// 执行结果
    /// </summary>
    public T? Result { get; set; }
}

/// <summary>
/// 单次重试尝试的记录
/// </summary>
public class RetryAttempt
{
    /// <summary>
    /// 尝试次数
    /// </summary>
    public int AttemptNumber { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 本次尝试的耗时
    /// </summary>
    public TimeSpan ElapsedTime { get; set; }

    /// <summary>
    /// 本次尝试的异常
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// 尝试时间
    /// </summary>
    public DateTime AttemptTime { get; set; } = DateTime.Now;
}

#endregion

#region 扩展方法

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

#endregion
