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

namespace XiHan.Framework.EventBus.SqlSugar.Outbox;

/// <summary>
/// 发件箱的 SqlSugar 实现
/// </summary>
public class SqlSugarEventOutbox : IEventOutbox
{
    private readonly ISqlSugarClientResolver _clientResolver;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<SqlSugarEventOutbox> _logger;
    private readonly XiHanSqlSugarEventBoxOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clientResolver">客户端解析器</param>
    /// <param name="currentTenant">当前租户</param>
    /// <param name="options">收发件箱存储配置</param>
    /// <param name="logger">日志器</param>
    public SqlSugarEventOutbox(
        ISqlSugarClientResolver clientResolver,
        ICurrentTenant currentTenant,
        IOptions<XiHanSqlSugarEventBoxOptions> options,
        ILogger<SqlSugarEventOutbox> logger)
    {
        _clientResolver = clientResolver;
        _currentTenant = currentTenant;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 将事件信息添加到发件箱
    /// </summary>
    /// <param name="outgoingEvent">出站事件信息</param>
    public async Task EnqueueAsync(OutgoingEventInfo outgoingEvent)
    {
        ArgumentNullException.ThrowIfNull(outgoingEvent);

        EnsureSharedLayout();

        var client = ResolveEnqueueClient();

        await client.Insertable(EventOutboxMapper.ToEntity(outgoingEvent)).ExecuteCommandAsync();
    }

    /// <summary>
    /// 确认当前租户与平台使用同一套数据库布局
    /// </summary>
    /// <remarks>
    /// 当前租户布局的主库与平台布局不同时抛出异常。
    /// </remarks>
    /// <exception cref="InvalidOperationException">当前租户使用独立于平台的数据库布局</exception>
    private void EnsureSharedLayout()
    {
        if (_currentTenant.Id is not > 0)
        {
            return;
        }

        var currentLayout = _clientResolver.GetCurrentLayoutConfigIds();

        IReadOnlyList<string> platformLayout;
        using (_currentTenant.Change(null))
        {
            platformLayout = _clientResolver.GetCurrentLayoutConfigIds();
        }

        if (currentLayout.Count == 0 || platformLayout.Count == 0 ||
            !string.Equals(currentLayout[0], platformLayout[0], StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"发件箱不支持租户独立库：当前租户 {_currentTenant.Id} 使用独立于平台的数据库布局，" +
                "写入其中的事件不会被发送循环投递。请改用共享库的隔离方式，或不要在该租户上下文中发布分布式事件。");
        }
    }

    /// <summary>
    /// 解析入箱写入的客户端
    /// </summary>
    /// <remarks>
    /// 当前工作单元已登记恰好一个连接时写该库，未登记任何连接时写当前库。
    /// </remarks>
    /// <returns>事件行写入的客户端</returns>
    /// <exception cref="InvalidOperationException">当前工作单元登记了多个连接</exception>
    private ISqlSugarClient ResolveEnqueueClient()
    {
        var enlistedConfigIds = _clientResolver.GetEnlistedConfigIds();

        if (enlistedConfigIds.Count == 0)
        {
            return _clientResolver.GetCurrentClient();
        }

        if (enlistedConfigIds.Count == 1)
        {
            return _clientResolver.GetClient(enlistedConfigIds[0]);
        }

        throw new InvalidOperationException(
            $"当前工作单元登记了多个数据库连接（{string.Join("、", enlistedConfigIds)}），无法确定事件应写入哪个库。" +
            "请拆分工作单元，使每个事务只写入一个库。");
    }

    /// <summary>
    /// 领取一批待发送的事件信息
    /// </summary>
    /// <remarks>
    /// 本方法在返回前会把记录标记为已领取，不是纯查询。
    /// 领取超时后记录可被重新领取，超时时长由 <see cref="XiHanSqlSugarEventBoxOptions.ClaimTimeout"/> 配置。
    /// 领取会遍历当前布局的全部库，每库最多领取 <c>maxCount</c> 除以库数的整数商，且每库至少领取 1 条；
    /// 库数超过 <c>maxCount</c> 时，单次领取的总量等于库数；某个库不可达时记录日志并跳过。
    /// </remarks>
    /// <param name="maxCount">最大数量</param>
    /// <param name="filter">过滤条件，本实现不支持，传入非空值将抛出异常</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>本次领取到的事件信息</returns>
    /// <exception cref="NotSupportedException"><paramref name="filter"/> 不为空</exception>
    public async Task<List<OutgoingEventInfo>> GetWaitingEventsAsync(
        int maxCount,
        Expression<Func<IOutgoingEventInfo, bool>>? filter = null,
        CancellationToken cancellationToken = default)
    {
        if (filter is not null)
        {
            throw new NotSupportedException(
                "SqlSugar 发件箱暂不支持 filter 参数，请改为在消费端筛选。");
        }

        if (maxCount <= 0)
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();

        var configIds = _clientResolver.GetCurrentLayoutConfigIds();
        if (configIds.Count == 0)
        {
            return [];
        }

        var quota = Math.Max(1, maxCount / configIds.Count);
        var claimed = new List<SysEventOutbox>();

        foreach (var configId in configIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var client = _clientResolver.GetClient(configId);

                claimed.AddRange(await ClaimFromDatabaseAsync(client, quota, cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从数据库 {ConfigId} 领取待发送事件失败，已跳过该库。", configId);
            }
        }

        return [.. claimed.OrderBy(item => item.CreatedTime).Select(EventOutboxMapper.ToEventInfo)];
    }

    /// <summary>
    /// 在指定库上领取一批待发送的记录
    /// </summary>
    /// <remarks>
    /// 选中的候选被其他实例抢先领走时另选一批重试，最多三轮；返回空集合表示该库确实没有可领取的记录。
    /// </remarks>
    /// <param name="client">该库的客户端</param>
    /// <param name="quota">本库最多领取的条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>本库领取到的记录</returns>
    private async Task<List<SysEventOutbox>> ClaimFromDatabaseAsync(
        ISqlSugarClient client,
        int quota,
        CancellationToken cancellationToken)
    {
        const int maxClaimAttempts = 3;

        for (var attempt = 0; attempt < maxClaimAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var now = DateTimeOffset.UtcNow;
            var staleBefore = now - _options.ClaimTimeout;
            var claimToken = Guid.NewGuid().ToString("N");

            var candidateIds = await client.Queryable<SysEventOutbox>()
                .Where(item => item.Status == SysEventOutbox.StatusPending
                    || (item.Status == SysEventOutbox.StatusClaimed && item.ClaimTime != null && item.ClaimTime < staleBefore))
                .OrderBy(item => item.CreatedTime)
                .Take(quota)
                .Select(item => item.BasicId)
                .ToListAsync(cancellationToken);

            if (candidateIds.Count == 0)
            {
                return [];
            }

            var affected = await client.Updateable<SysEventOutbox>()
                .SetColumns(item => new SysEventOutbox
                {
                    Status = SysEventOutbox.StatusClaimed,
                    ClaimToken = claimToken,
                    ClaimTime = now
                })
                .Where(item => candidateIds.Contains(item.BasicId)
                    && (item.Status == SysEventOutbox.StatusPending
                        || (item.Status == SysEventOutbox.StatusClaimed && item.ClaimTime != null && item.ClaimTime < staleBefore)))
                .ExecuteCommandAsync(cancellationToken);

            // 候选全被其他实例抢走，另选一批重试
            if (affected == 0)
            {
                continue;
            }

            return await client.Queryable<SysEventOutbox>()
                .Where(item => item.ClaimToken == claimToken)
                .OrderBy(item => item.CreatedTime)
                .ToListAsync(cancellationToken);
        }

        return [];
    }

    /// <summary>
    /// 删除指定的事件信息
    /// </summary>
    /// <param name="id">事件唯一标识符</param>
    public async Task DeleteAsync(Guid id)
    {
        await DeleteManyAsync([id]);
    }

    /// <summary>
    /// 批量删除事件信息
    /// </summary>
    /// <remarks>
    /// 删除会遍历当前布局的全部库；主键全局唯一，没有该记录的库上执行只删除 0 行。
    /// 某个库删除失败时记录日志并跳过该库，不向调用方传播异常；该库上的记录保持可领取状态，
    /// 会在后续轮询中被重新领取并再次投递。
    /// </remarks>
    /// <param name="ids">事件唯一标识符集合</param>
    public async Task DeleteManyAsync(IEnumerable<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var idList = ids.ToList();
        if (idList.Count == 0)
        {
            return;
        }

        foreach (var configId in _clientResolver.GetCurrentLayoutConfigIds())
        {
            try
            {
                var client = _clientResolver.GetClient(configId);

                await client.Deleteable<SysEventOutbox>().In(idList).ExecuteCommandAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从数据库 {ConfigId} 删除已投递事件失败，已跳过该库。", configId);
            }
        }
    }
}
