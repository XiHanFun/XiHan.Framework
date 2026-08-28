// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Tasks.BackgroundServices;

namespace XiHan.Framework.Tasks.Tests.BackgroundServices.Fakes;

/// <summary>
/// 任务体挂在闸门上的后台服务替身
/// </summary>
/// <remarks>
/// 与 <see cref="RecordingBackgroundService"/> 的"挂到取消为止"不同，这里的任务体只等一道由用例手动打开的闸门，
/// <b>不响应取消令牌</b>。这样一来停机时在途任务必然还留在基类的在途集合里，
/// "等在途任务收尾"那段逻辑一定会被真正走到，而不是靠"任务恰好还没跑完"的时序碰运气。
/// </remarks>
public sealed class GatedBackgroundService : XiHanBackgroundServiceBase<GatedBackgroundService>
{
    private readonly object _gate = new();
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Queue<List<IBackgroundTaskItem>> _batches = new();
    private readonly List<string> _startedTaskIds = [];

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="options">配置选项</param>
    public GatedBackgroundService(
        ILogger<GatedBackgroundService> logger,
        IOptions<XiHanBackgroundServiceOptions> options)
        : base(logger, options)
    {
    }

    /// <summary>
    /// 已进入处理（即已挂在闸门上）的任务标识
    /// </summary>
    public IReadOnlyList<string> StartedTaskIds
    {
        get
        {
            lock (_gate)
            {
                return [.. _startedTaskIds];
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
    /// 打开闸门，放在途任务收尾
    /// </summary>
    public void ReleaseGate()
    {
        _release.TrySetResult();
    }

    /// <summary>
    /// 批量抽取任务：投喂完毕后一律返回空列表
    /// </summary>
    /// <param name="maxCount">最大获取数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务项列表</returns>
    protected override Task<IEnumerable<IBackgroundTaskItem>> FetchWorkItemsAsync(int maxCount, CancellationToken cancellationToken)
    {
        List<IBackgroundTaskItem> batch;

        lock (_gate)
        {
            batch = _batches.Count > 0 ? _batches.Dequeue() : [];
        }

        return Task.FromResult<IEnumerable<IBackgroundTaskItem>>(batch);
    }

    /// <summary>
    /// 处理单个任务：一直等到闸门打开，期间不理会取消令牌
    /// </summary>
    /// <param name="item">任务项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    protected override async Task ProcessItemAsync(IBackgroundTaskItem item, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _startedTaskIds.Add(item.TaskId);
        }

        await _release.Task;
    }
}
