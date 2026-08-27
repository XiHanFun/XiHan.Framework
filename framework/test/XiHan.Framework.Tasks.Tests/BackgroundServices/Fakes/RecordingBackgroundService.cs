// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Tasks.BackgroundServices;
using XiHan.Framework.Utils.Diagnostics.RetryPolicys;

namespace XiHan.Framework.Tasks.Tests.BackgroundServices.Fakes;

/// <summary>
/// 用于验证后台服务基类模板方法的最小具体子类
/// </summary>
/// <remarks>
/// 只做三件事：按批投喂任务、记录每次抽取与处理的入参、把失败回调的异常记下来。
/// 任务体默认瞬时完成；打开 <see cref="BlockUntilCancelled"/> 后会一直挂起到取消令牌触发，
/// 用来验证取消令牌确实从停止令牌链下来。
/// </remarks>
public sealed class RecordingBackgroundService : XiHanBackgroundServiceBase<RecordingBackgroundService>
{
    private readonly object _gate = new();
    private readonly Queue<List<IBackgroundTaskItem>> _batches = new();
    private readonly List<string> _processedTaskIds = [];
    private readonly List<int> _requestedMaxCounts = [];
    private readonly List<Exception> _failures = [];
    private readonly List<ConfigChangedEventArgs> _configChanges = [];
    private int _fetchCallCount;
    private CancellationToken _lastProcessToken;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="options">配置选项</param>
    /// <param name="dynamicConfig">动态配置管理器</param>
    /// <param name="retryPolicy">重试策略</param>
    public RecordingBackgroundService(
        ILogger<RecordingBackgroundService> logger,
        IOptions<XiHanBackgroundServiceOptions> options,
        IDynamicServiceConfig? dynamicConfig = null,
        RetryPolicy? retryPolicy = null)
        : base(logger, options, dynamicConfig, retryPolicy)
    {
    }

    /// <summary>
    /// 处理任务时抛出的异常（为空表示处理成功）
    /// </summary>
    public Exception? ProcessException { get; set; }

    /// <summary>
    /// 处理任务时是否一直挂起到取消
    /// </summary>
    public bool BlockUntilCancelled { get; set; }

    /// <summary>
    /// 抽取任务的调用次数
    /// </summary>
    public int FetchCallCount
    {
        get
        {
            lock (_gate)
            {
                return _fetchCallCount;
            }
        }
    }

    /// <summary>
    /// 历次抽取任务时传入的最大数量
    /// </summary>
    public IReadOnlyList<int> RequestedMaxCounts
    {
        get
        {
            lock (_gate)
            {
                return [.. _requestedMaxCounts];
            }
        }
    }

    /// <summary>
    /// 已进入处理的任务标识
    /// </summary>
    public IReadOnlyList<string> ProcessedTaskIds
    {
        get
        {
            lock (_gate)
            {
                return [.. _processedTaskIds];
            }
        }
    }

    /// <summary>
    /// 失败回调收到的异常
    /// </summary>
    public IReadOnlyList<Exception> Failures
    {
        get
        {
            lock (_gate)
            {
                return [.. _failures];
            }
        }
    }

    /// <summary>
    /// 收到的配置变更事件
    /// </summary>
    public IReadOnlyList<ConfigChangedEventArgs> ConfigChanges
    {
        get
        {
            lock (_gate)
            {
                return [.. _configChanges];
            }
        }
    }

    /// <summary>
    /// 最近一次传给任务处理方法的取消令牌
    /// </summary>
    public CancellationToken LastProcessToken
    {
        get
        {
            lock (_gate)
            {
                return _lastProcessToken;
            }
        }
    }

    /// <summary>
    /// 追加一批待抽取的任务
    /// </summary>
    /// <param name="items">任务项</param>
    public void EnqueueBatch(params IBackgroundTaskItem[] items)
    {
        lock (_gate)
        {
            _batches.Enqueue([.. items]);
        }
    }

    /// <summary>
    /// 批量抽取任务：投喂完毕后一律返回空列表，让主循环进入空闲分支
    /// </summary>
    /// <param name="maxCount">最大获取数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务项列表</returns>
    protected override Task<IEnumerable<IBackgroundTaskItem>> FetchWorkItemsAsync(int maxCount, CancellationToken cancellationToken)
    {
        List<IBackgroundTaskItem> batch;

        lock (_gate)
        {
            _fetchCallCount++;
            _requestedMaxCounts.Add(maxCount);
            batch = _batches.Count > 0 ? _batches.Dequeue() : new List<IBackgroundTaskItem>();
        }

        return Task.FromResult<IEnumerable<IBackgroundTaskItem>>(batch);
    }

    /// <summary>
    /// 处理单个任务
    /// </summary>
    /// <param name="item">任务项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    protected override async Task ProcessItemAsync(IBackgroundTaskItem item, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _processedTaskIds.Add(item.TaskId);
            _lastProcessToken = cancellationToken;
        }

        if (BlockUntilCancelled)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        if (ProcessException is not null)
        {
            throw ProcessException;
        }
    }

    /// <summary>
    /// 任务最终失败回调
    /// </summary>
    /// <param name="item">失败的任务项</param>
    /// <param name="exception">异常信息</param>
    protected override void OnTaskFailed(IBackgroundTaskItem item, Exception exception)
    {
        lock (_gate)
        {
            _failures.Add(exception);
        }

        base.OnTaskFailed(item, exception);
    }

    /// <summary>
    /// 配置变更回调
    /// </summary>
    /// <param name="sender">发送者</param>
    /// <param name="e">事件参数</param>
    protected override void OnConfigChanged(object? sender, ConfigChangedEventArgs e)
    {
        lock (_gate)
        {
            _configChanges.Add(e);
        }

        base.OnConfigChanged(sender, e);
    }
}
