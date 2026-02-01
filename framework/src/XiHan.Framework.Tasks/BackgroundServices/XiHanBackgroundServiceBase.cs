#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanBackgroundServiceBase
// Guid:81aef5eb-a36b-4162-99af-598d418258bc
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/17 14:59:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using XiHan.Framework.Tasks.BackgroundServices.Abstractions;
using XiHan.Framework.Utils.Diagnostics.RetryPolicys;

namespace XiHan.Framework.Tasks.BackgroundServices;

/// <summary>
/// 后台服务基类
/// 提供并发任务管理、队列处理、异常重试等核心功能
/// </summary>
/// <typeparam name="T">具体的后台服务类型</typeparam>
public abstract class XiHanBackgroundServiceBase<T> : BackgroundService, IBackgroundWorker where T : class
{
    /// <summary>
    /// 日志记录器
    /// </summary>
    protected readonly ILogger<T> Logger;

    /// <summary>
    /// 配置选项
    /// </summary>
    protected readonly XiHanBackgroundServiceOptions Options;

    /// <summary>
    /// 动态配置管理器
    /// </summary>
    protected readonly IDynamicServiceConfig DynamicConfig;

    /// <summary>
    /// 重试策略
    /// </summary>
    protected readonly RetryPolicy? RetryPolicy;

    /// <summary>
    /// 统计信息
    /// </summary>
    protected readonly BackgroundServiceStatistics Statistics = new();

    private readonly ConcurrentDictionary<string, Task> _runningTasks = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="options">配置选项</param>
    /// <param name="dynamicConfig">动态配置管理器（可选）</param>
    /// <param name="retryPolicy">重试策略（可选）</param>
    protected XiHanBackgroundServiceBase(
        ILogger<T> logger,
        IOptions<XiHanBackgroundServiceOptions> options,
        IDynamicServiceConfig? dynamicConfig = null,
        RetryPolicy? retryPolicy = null)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        // 创建动态配置，如果没有提供则使用默认实现
        DynamicConfig = dynamicConfig ?? new DynamicServiceConfig(options);

        // 设置重试策略
        RetryPolicy = retryPolicy ?? (Options.EnableRetry ? CreateDefaultRetryPolicy() : null);

        // 监听配置变更
        DynamicConfig.ConfigChanged += OnConfigChanged;
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public override void Dispose()
    {
        DynamicConfig.ConfigChanged -= OnConfigChanged;
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 获取服务统计信息
    /// </summary>
    /// <returns>统计信息摘要</returns>
    public StatisticsSummary GetStatistics()
    {
        return Statistics.GetSummary();
    }

    /// <summary>
    /// 获取动态配置管理器
    /// </summary>
    /// <returns>动态配置</returns>
    public IDynamicServiceConfig GetDynamicConfig()
    {
        return DynamicConfig;
    }

    /// <summary>
    /// 获取当前服务状态信息
    /// </summary>
    /// <returns>服务状态</returns>
    public XiHanBackgroundServiceStatusInfo GetServiceStatus()
    {
        return new XiHanBackgroundServiceStatusInfo
        {
            ServiceName = typeof(T).Name,
            IsTaskProcessingEnabled = DynamicConfig.IsTaskProcessingEnabled,
            MaxConcurrentTasks = DynamicConfig.MaxConcurrentTasks,
            CurrentRunningTasks = _runningTasks.Count,
            IdleDelayMilliseconds = DynamicConfig.IdleDelayMilliseconds,
            RetryEnabled = RetryPolicy != null,
            Statistics = Statistics.GetSummary()
        };
    }

    /// <summary>
    /// 从队列/消息源批量获取任务
    /// 子类需要实现具体的获取逻辑（Redis、RabbitMQ、数据库等）
    /// </summary>
    /// <param name="maxCount">最大获取数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务项列表，空列表表示当前无任务</returns>
    protected abstract Task<IEnumerable<IBackgroundTaskItem>> FetchWorkItemsAsync(int maxCount, CancellationToken cancellationToken);

    /// <summary>
    /// 处理单个任务
    /// 子类需要实现具体的处理逻辑
    /// </summary>
    /// <param name="item">任务项</param>
    /// <param name="cancellationToken">取消令牌</param>
    protected abstract Task ProcessItemAsync(IBackgroundTaskItem item, CancellationToken cancellationToken);

    /// <summary>
    /// 任务处理失败时的回调（可选实现）
    /// </summary>
    /// <param name="item">失败的任务项</param>
    /// <param name="exception">异常信息</param>
    protected virtual void OnTaskFailed(IBackgroundTaskItem item, Exception exception)
    {
        Logger.LogError(exception, "任务 {TaskId} 处理失败", item.TaskId);
    }

    /// <summary>
    /// 创建默认重试策略
    /// </summary>
    /// <returns>重试策略</returns>
    protected virtual RetryPolicy CreateDefaultRetryPolicy()
    {
        return RetryPolicyFactory.WithExponentialBackoff(
            Options.MaxRetryCount,
            TimeSpan.FromMilliseconds(Options.RetryDelayMilliseconds),
            backoffMultiplier: 2.0,
            maxDelay: TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// 配置变更事件处理
    /// </summary>
    /// <param name="sender">发送者</param>
    /// <param name="e">事件参数</param>
    protected virtual void OnConfigChanged(object? sender, ConfigChangedEventArgs e)
    {
        Logger.LogInformation("配置项 {PropertyName} 从 {OldValue} 变更为 {NewValue}",
            e.PropertyName, e.OldValue, e.NewValue);
    }

    /// <summary>
    /// 执行后台服务的主逻辑
    /// </summary>
    /// <param name="stoppingToken">停止令牌</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("后台服务 {ServiceType} 已启动，最大并发数: {MaxConcurrentTasks}，重试策略: {RetryEnabled}",
            typeof(T).Name, DynamicConfig.MaxConcurrentTasks, RetryPolicy != null ? "启用" : "禁用");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 检查是否启用任务处理
                    if (!DynamicConfig.IsTaskProcessingEnabled)
                    {
                        await Task.Delay(DynamicConfig.IdleDelayMilliseconds, stoppingToken);
                        continue;
                    }

                    var running = _runningTasks.Count;
                    var maxConcurrent = DynamicConfig.MaxConcurrentTasks;

                    if (running < maxConcurrent)
                    {
                        var items = await FetchWorkItemsAsync(maxConcurrent - running, stoppingToken);
                        if (items.Any())
                        {
                            foreach (var item in items)
                            {
                                var taskId = item.TaskId;
                                var task = ProcessItemWithRetryAsync(item, stoppingToken);
                                _runningTasks.TryAdd(taskId, task);
                            }
                        }
                        else
                        {
                            // 没有任务，等待一段时间
                            await Task.Delay(DynamicConfig.IdleDelayMilliseconds, stoppingToken);
                        }
                    }
                    else
                    {
                        await Task.Delay(DynamicConfig.IdleDelayMilliseconds, stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "后台服务执行异常");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        finally
        {
            Logger.LogInformation("后台服务 {ServiceType} 停止，等待 {Count} 个任务收尾", typeof(T).Name, _runningTasks.Count);

            // 等待所有任务完成，带超时控制
            try
            {
                await Task.WhenAll(_runningTasks.Values).WaitAsync(
                    TimeSpan.FromMilliseconds(Options.ShutdownTimeoutMilliseconds), stoppingToken);
            }
            catch (TimeoutException)
            {
                Logger.LogWarning("等待任务完成超时，强制停止服务");
            }

            Logger.LogInformation("后台服务 {ServiceType} 已停止", typeof(T).Name);
        }
    }

    /// <summary>
    /// 带重试策略的任务处理包装器
    /// </summary>
    /// <param name="item">任务项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>处理任务</returns>
    private Task ProcessItemWithRetryAsync(IBackgroundTaskItem item, CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            var startTime = DateTimeOffset.UtcNow;
            var success = false;

            Statistics.RecordTaskStarted();

            try
            {
                using var timeoutCts = Options.EnableTaskTimeout && Options.TaskTimeoutMilliseconds > 0
                    ? new CancellationTokenSource(Options.TaskTimeoutMilliseconds)
                    : null;

                using var combinedCts = timeoutCts != null
                    ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token)
                    : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                if (RetryPolicy != null)
                {
                    // 使用重试策略执行任务
                    var retryResult = await RetryPolicy.ExecuteAsync(async () =>
                    {
                        await ProcessItemAsync(item, combinedCts.Token);
                    });

                    // 记录重试次数
                    if (retryResult.TotalAttempts > 1)
                    {
                        for (var i = 1; i < retryResult.TotalAttempts; i++)
                        {
                            Statistics.RecordTaskRetried();
                        }
                        Logger.LogWarning("任务 {TaskId} 经过 {Attempts} 次尝试后完成", item.TaskId, retryResult.TotalAttempts);
                    }

                    if (!retryResult.IsSuccess && retryResult.Exception != null)
                    {
                        throw retryResult.Exception;
                    }
                }
                else
                {
                    // 直接执行任务，不重试
                    await ProcessItemAsync(item, combinedCts.Token);
                }

                success = true;
                Logger.LogDebug("任务 {TaskId} 处理成功", item.TaskId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Logger.LogWarning("任务 {TaskId} 被取消", item.TaskId);
            }
            catch (Exception ex)
            {
                OnTaskFailed(item, ex);
            }
            finally
            {
                // 记录统计信息
                var processingTime = (long)(DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
                Statistics.RecordTaskCompleted(item.TaskId, processingTime, success);

                // 清理任务
                _runningTasks.TryRemove(item.TaskId, out _);
            }
        }, cancellationToken);
    }
}
