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

namespace XiHan.Framework.Utils.Diagnostics.RetryPolicys;

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
}
