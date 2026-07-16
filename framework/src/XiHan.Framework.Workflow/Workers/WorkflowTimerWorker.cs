#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WorkflowTimerWorker
// Guid:2f95c0d8-64ba-4e17-93a5-d80c26e51f39
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 11:25:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Caching.Distributed.Abstracts;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Timing;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Exceptions;
using XiHan.Framework.Workflow.Abstractions.Stores;
using XiHan.Framework.Workflow.Options;

namespace XiHan.Framework.Workflow.Workers;

/// <summary>
/// 工作流定时器 Worker（轮询到期书签并恢复实例：延时、重试、节点超时）
/// </summary>
/// <remarks>
/// 集群内通过分布式锁保证单活轮询；作用域服务按轮解析；
/// 单个书签恢复失败仅记录日志，不影响其余书签与后续轮次。
/// </remarks>
public class WorkflowTimerWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDistributedLock _distributedLock;
    private readonly ILogger<WorkflowTimerWorker> _logger;
    private readonly XiHanWorkflowWorkerOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="scopeFactory">服务作用域工厂</param>
    /// <param name="distributedLock">分布式锁</param>
    /// <param name="options">Worker 选项</param>
    /// <param name="logger">日志记录器</param>
    public WorkflowTimerWorker(
        IServiceScopeFactory scopeFactory,
        IDistributedLock distributedLock,
        IOptions<XiHanWorkflowWorkerOptions> options,
        ILogger<WorkflowTimerWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _distributedLock = distributedLock;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// 执行轮询主循环
    /// </summary>
    /// <param name="stoppingToken">停止令牌</param>
    /// <returns>任务</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.IsTimerEnabled)
        {
            _logger.LogInformation("工作流定时器已关闭（IsTimerEnabled=false），Worker 空转退出");
            return;
        }

        try
        {
            await Task.Delay(_options.FirstWaitDurationMilliseconds, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_options.PollPeriodMilliseconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }

                await PollOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // 永不因单轮异常而崩溃
                _logger.LogError(ex, "工作流定时器轮询发生异常");
            }
        }
    }

    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        await using var handle = await _distributedLock.TryAcquireAsync(
            _options.DistributedLockName,
            TimeSpan.FromSeconds(_options.DistributedLockExpirySeconds),
            cancellationToken);

        if (handle is null)
        {
            // 其它实例正在轮询，本轮跳过
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        var bookmarkStore = serviceProvider.GetRequiredService<IWorkflowBookmarkStore>();
        var clock = serviceProvider.GetRequiredService<IClock>();
        var engine = serviceProvider.GetRequiredService<IWorkflowEngine>();
        var currentTenant = serviceProvider.GetRequiredService<ICurrentTenant>();

        var dueBookmarks = await bookmarkStore.GetDueAsync(clock.Now, _options.MaxBookmarkFetchCount, cancellationToken);
        if (dueBookmarks.Count == 0)
        {
            return;
        }

        foreach (var bookmark in dueBookmarks)
        {
            try
            {
                using (currentTenant.Change(bookmark.TenantId))
                {
                    // 实例挂起/故障时到期回退并保留书签，待实例恢复后重投
                    await engine.ResumeBookmarkAsync(
                        bookmark.Id, inputs: null, throwIfNotResumable: false, expectedBookmarkKey: null, cancellationToken);
                }
            }
            catch (WorkflowLockTimeoutException ex)
            {
                // 锁竞争是瞬态冲突，书签未消费，下轮轮询自动重试
                _logger.LogDebug(ex, "到期书签 {BookmarkId} 遇到实例锁竞争，下轮重试", bookmark.Id);
            }
            catch (WorkflowException ex)
            {
                _logger.LogDebug(ex, "到期书签 {BookmarkId} 已被并发处理或已清理，跳过", bookmark.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "到期书签 {BookmarkId}（实例 {InstanceId}）恢复失败", bookmark.Id, bookmark.InstanceId);
            }
        }
    }
}
