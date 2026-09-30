// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.SqlSugar.Entities;
using XiHan.Framework.EventBus.SqlSugar.Mapping;
using XiHan.Framework.EventBus.SqlSugar.Options;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.EventBus.SqlSugar.Inbox;

/// <summary>
/// 收件箱的 SqlSugar 实现
/// </summary>
/// <remarks>
/// 所有读写都切换到无租户上下文，落在宿主布局的主库。
/// </remarks>
public class SqlSugarEventInbox : IEventInbox
{
    private readonly ISqlSugarClientResolver _clientResolver;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<SqlSugarEventInbox> _logger;
    private readonly XiHanSqlSugarEventBoxOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clientResolver">客户端解析器</param>
    /// <param name="currentTenant">当前租户</param>
    /// <param name="options">收发件箱存储配置</param>
    /// <param name="logger">日志器</param>
    public SqlSugarEventInbox(
        ISqlSugarClientResolver clientResolver,
        ICurrentTenant currentTenant,
        IOptions<XiHanSqlSugarEventBoxOptions> options,
        ILogger<SqlSugarEventInbox> logger)
    {
        _clientResolver = clientResolver;
        _currentTenant = currentTenant;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 将事件信息添加到收件箱
    /// </summary>
    /// <remarks>
    /// 去重键已在库时不写入、不抛异常；写入因其他原因失败时原样抛出。
    /// </remarks>
    /// <param name="incomingEvent">入站事件信息</param>
    public async Task EnqueueAsync(IncomingEventInfo incomingEvent)
    {
        ArgumentNullException.ThrowIfNull(incomingEvent);

        var entity = EventInboxMapper.ToEntity(incomingEvent);
        var dedupKey = entity.DedupKey;

        using (_currentTenant.Change(null))
        {
            var client = _clientResolver.GetCurrentClient();

            try
            {
                await client.Insertable(entity).ExecuteCommandAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var duplicated = await client.Queryable<SysEventInbox>()
                    .AnyAsync(item => item.DedupKey == dedupKey);

                if (!duplicated)
                {
                    throw;
                }

                _logger.LogDebug(ex, "收件箱已存在去重键为 {DedupKey} 的记录，本次入箱已忽略。", dedupKey);
            }
        }
    }

    /// <summary>
    /// 检查消息标识符是否存在
    /// </summary>
    /// <remarks>
    /// 不区分记录状态，已处理与已丢弃的记录同样视为存在。
    /// </remarks>
    /// <param name="messageId">消息标识符</param>
    /// <returns>存在返回 true</returns>
    public async Task<bool> ExistsByMessageIdAsync(string messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return false;
        }

        using (_currentTenant.Change(null))
        {
            var client = _clientResolver.GetCurrentClient();

            return await client.Queryable<SysEventInbox>()
                .AnyAsync(item => item.DedupKey == messageId);
        }
    }

    /// <summary>
    /// 领取一批待处理的事件信息
    /// </summary>
    /// <remarks>
    /// 本方法在返回前会把记录标记为已领取，不是纯查询。
    /// 可领取的记录是：待处理且未设下次重试时刻或该时刻已到的记录，以及领取已超时的记录；
    /// 超时时长由 <see cref="XiHanSqlSugarEventBoxOptions.ClaimTimeout"/> 配置。
    /// </remarks>
    /// <param name="maxCount">最大数量</param>
    /// <param name="filter">过滤条件，本实现不支持，传入非空值将抛出异常</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>本次领取到的事件信息</returns>
    /// <exception cref="NotSupportedException"><paramref name="filter"/> 不为空</exception>
    public async Task<List<IncomingEventInfo>> GetWaitingEventsAsync(
        int maxCount,
        Expression<Func<IIncomingEventInfo, bool>>? filter = null,
        CancellationToken cancellationToken = default)
    {
        if (filter is not null)
        {
            throw new NotSupportedException(
                "SqlSugar 收件箱暂不支持 filter 参数，请改为在事件处理器内筛选。");
        }

        if (maxCount <= 0)
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();

        using (_currentTenant.Change(null))
        {
            var client = _clientResolver.GetCurrentClient();

            var claimed = await ClaimAsync(client, maxCount, cancellationToken);

            return [.. claimed.Select(EventInboxMapper.ToEventInfo)];
        }
    }

    /// <summary>
    /// 标记事件为已处理
    /// </summary>
    /// <remarks>
    /// 记录保留在库中，保留期满后由 <see cref="DeleteOldEventsAsync"/> 清理。
    /// </remarks>
    /// <param name="id">事件唯一标识符</param>
    public async Task MarkAsProcessedAsync(Guid id)
    {
        await MarkAsHandledAsync(id, SysEventInbox.StatusProcessed);
    }

    /// <summary>
    /// 延迟处理事件
    /// </summary>
    /// <remarks>
    /// 记录放回待处理并清空领取信息，下次重试时刻为空时立即可领取。
    /// </remarks>
    /// <param name="id">事件唯一标识符</param>
    /// <param name="retryCount">重试次数</param>
    /// <param name="nextRetryTime">下次重试时间</param>
    public async Task RetryLaterAsync(Guid id, int retryCount, DateTime? nextRetryTime)
    {
        var nextRetry = nextRetryTime.HasValue
            ? EventInboxMapper.ToOffset(nextRetryTime.Value)
            : DateTimeOffset.UtcNow;

        using (_currentTenant.Change(null))
        {
            var client = _clientResolver.GetCurrentClient();

            await client.Updateable<SysEventInbox>()
                .SetColumns(item => new SysEventInbox
                {
                    Status = SysEventInbox.StatusPending,
                    RetryCount = retryCount,
                    NextRetryTime = nextRetry,
                    ClaimToken = null,
                    ClaimTime = null
                })
                .Where(item => item.BasicId == id && item.Status == SysEventInbox.StatusClaimed)
                .ExecuteCommandAsync();
        }
    }

    /// <summary>
    /// 标记事件为已丢弃
    /// </summary>
    /// <remarks>
    /// 记录保留在库中，保留期满后由 <see cref="DeleteOldEventsAsync"/> 清理。
    /// </remarks>
    /// <param name="id">事件唯一标识</param>
    public async Task MarkAsDiscardAsync(Guid id)
    {
        await MarkAsHandledAsync(id, SysEventInbox.StatusDiscarded);
    }

    /// <summary>
    /// 删除过期事件
    /// </summary>
    /// <remarks>
    /// 只删除已处理或已丢弃、且完结时刻早于保留期的记录，保留期由
    /// <see cref="XiHanSqlSugarEventBoxOptions.InboxRetentionPeriod"/> 配置。
    /// </remarks>
    public async Task DeleteOldEventsAsync()
    {
        var cutoff = DateTimeOffset.UtcNow - _options.InboxRetentionPeriod;

        using (_currentTenant.Change(null))
        {
            var client = _clientResolver.GetCurrentClient();

            await client.Deleteable<SysEventInbox>()
                .Where(item => (item.Status == SysEventInbox.StatusProcessed || item.Status == SysEventInbox.StatusDiscarded)
                    && item.HandledTime != null
                    && item.HandledTime <= cutoff)
                .ExecuteCommandAsync();
        }
    }

    /// <summary>
    /// 把记录置为完结状态并清空领取与重试信息
    /// </summary>
    /// <param name="id">事件唯一标识符</param>
    /// <param name="status">完结状态</param>
    private async Task MarkAsHandledAsync(Guid id, int status)
    {
        var now = DateTimeOffset.UtcNow;

        using (_currentTenant.Change(null))
        {
            var client = _clientResolver.GetCurrentClient();

            await client.Updateable<SysEventInbox>()
                .SetColumns(item => new SysEventInbox
                {
                    Status = status,
                    NextRetryTime = null,
                    ClaimToken = null,
                    ClaimTime = null,
                    HandledTime = now
                })
                .Where(item => item.BasicId == id && item.Status == SysEventInbox.StatusClaimed)
                .ExecuteCommandAsync();
        }
    }

    /// <summary>
    /// 在指定库上领取一批待处理的记录
    /// </summary>
    /// <remarks>
    /// 选中的候选被其他实例抢先领走时另选一批重试，最多三轮；返回空集合表示确实没有可领取的记录。
    /// </remarks>
    /// <param name="client">客户端</param>
    /// <param name="maxCount">最多领取的条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>领取到的记录</returns>
    private async Task<List<SysEventInbox>> ClaimAsync(
        ISqlSugarClient client,
        int maxCount,
        CancellationToken cancellationToken)
    {
        const int maxClaimAttempts = 3;

        for (var attempt = 0; attempt < maxClaimAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var now = DateTimeOffset.UtcNow;
            var staleBefore = now - _options.ClaimTimeout;
            var claimToken = Guid.NewGuid().ToString("N");

            var candidateIds = await client.Queryable<SysEventInbox>()
                .Where(item => (item.Status == SysEventInbox.StatusPending && (item.NextRetryTime == null || item.NextRetryTime <= now))
                    || (item.Status == SysEventInbox.StatusClaimed && item.ClaimTime != null && item.ClaimTime < staleBefore))
                .OrderBy(item => item.CreatedTime)
                .Take(maxCount)
                .Select(item => item.BasicId)
                .ToListAsync(cancellationToken);

            if (candidateIds.Count == 0)
            {
                return [];
            }

            var affected = await client.Updateable<SysEventInbox>()
                .SetColumns(item => new SysEventInbox
                {
                    Status = SysEventInbox.StatusClaimed,
                    ClaimToken = claimToken,
                    ClaimTime = now
                })
                .Where(item => candidateIds.Contains(item.BasicId)
                    && ((item.Status == SysEventInbox.StatusPending && (item.NextRetryTime == null || item.NextRetryTime <= now))
                        || (item.Status == SysEventInbox.StatusClaimed && item.ClaimTime != null && item.ClaimTime < staleBefore)))
                .ExecuteCommandAsync(cancellationToken);

            // 候选全被其他实例抢走，另选一批重试
            if (affected == 0)
            {
                continue;
            }

            return await client.Queryable<SysEventInbox>()
                .Where(item => item.ClaimToken == claimToken)
                .OrderBy(item => item.CreatedTime)
                .ToListAsync(cancellationToken);
        }

        return [];
    }
}
