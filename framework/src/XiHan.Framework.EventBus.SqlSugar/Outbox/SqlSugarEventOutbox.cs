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
using XiHan.Framework.Uow;
using XiHan.Framework.Uow.Options;

namespace XiHan.Framework.EventBus.SqlSugar.Outbox;

/// <summary>
/// 发件箱的 SqlSugar 实现
/// </summary>
/// <remarks>
/// 读写都作用于当前租户上下文所在的数据库布局；租户上下文中领取、按标识遍历删除与待送数统计只作用于租户独立的库，
/// 平台布局内的库由宿主上下文负责。
/// 本实例记住自己领取的每条事件来自哪个库、用了哪个领取令牌，删除这些事件时只在该库按该令牌删除。
/// </remarks>
public class SqlSugarEventOutbox : ITenantScopedEventOutbox
{
    private readonly ISqlSugarClientResolver _clientResolver;
    private readonly ICurrentTenant _currentTenant;
    private readonly ISqlSugarOutboxConnectionScope _connectionScope;
    private readonly IOutboxDeliveryTargetProvider? _targetProvider;
    private readonly IUnitOfWorkManager? _unitOfWorkManager;
    private readonly ILogger<SqlSugarEventOutbox> _logger;
    private readonly XiHanSqlSugarEventBoxOptions _options;

    private const int MaxClaimPasses = 2;

    private static int _claimRotation;

    private readonly Dictionary<Guid, ClaimSource> _claimSources = [];

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clientResolver">客户端解析器</param>
    /// <param name="currentTenant">当前租户</param>
    /// <param name="connectionScope">入箱连接范围</param>
    /// <param name="targetProviders">发件箱投递目标目录，未注册时为空集合，注册多个时取最后一个</param>
    /// <param name="options">收发件箱存储配置</param>
    /// <param name="logger">日志器</param>
    /// <param name="unitOfWorkManager">工作单元管理器，为 null 时目录查询沿用当前工作单元</param>
    public SqlSugarEventOutbox(
        ISqlSugarClientResolver clientResolver,
        ICurrentTenant currentTenant,
        ISqlSugarOutboxConnectionScope connectionScope,
        IEnumerable<IOutboxDeliveryTargetProvider> targetProviders,
        IOptions<XiHanSqlSugarEventBoxOptions> options,
        ILogger<SqlSugarEventOutbox> logger,
        IUnitOfWorkManager? unitOfWorkManager = null)
    {
        ArgumentNullException.ThrowIfNull(targetProviders);

        _clientResolver = clientResolver;
        _currentTenant = currentTenant;
        _connectionScope = connectionScope;
        _targetProvider = targetProviders.LastOrDefault();
        _options = options.Value;
        _logger = logger;
        _unitOfWorkManager = unitOfWorkManager;
    }

    /// <summary>
    /// 将事件信息添加到发件箱
    /// </summary>
    /// <remarks>
    /// 落点：入箱连接范围指定了连接时写该连接，该连接必须已登记在当前工作单元；
    /// 未指定时，当前工作单元恰好登记一个连接则写该库，一个都没有则写当前布局的主库，多于一个则拒绝。
    /// 处于租户上下文时，落点须在平台布局内，或该租户在投递目标目录中启用且落点属于该租户的布局；
    /// 目录中已停用的租户一律拒绝入箱。
    /// </remarks>
    /// <param name="outgoingEvent">出站事件信息</param>
    /// <exception cref="InvalidOperationException">无法确定落点，或写入落点的事件不会被发送循环投递</exception>
    public async Task EnqueueAsync(OutgoingEventInfo outgoingEvent)
    {
        ArgumentNullException.ThrowIfNull(outgoingEvent);

        var configId = ResolveEnqueueConfigId();

        await EnsureDeliverableAsync(configId);

        var client = _clientResolver.GetClient(configId);

        await client.Insertable(EventOutboxMapper.ToEntity(outgoingEvent)).ExecuteCommandAsync();
    }

    /// <summary>
    /// 解析入箱写入的连接配置标识
    /// </summary>
    /// <returns>连接配置标识</returns>
    /// <exception cref="InvalidOperationException">指定的连接未登记、登记了多个连接却未指定，或无法解析当前布局</exception>
    private string ResolveEnqueueConfigId()
    {
        var enlistedConfigIds = _clientResolver.GetEnlistedConfigIds();
        var specifiedConfigId = _connectionScope.ConfigId;

        if (specifiedConfigId is not null)
        {
            if (!enlistedConfigIds.Contains(specifiedConfigId, StringComparer.Ordinal))
            {
                var enlisted = enlistedConfigIds.Count == 0 ? "无" : string.Join("、", enlistedConfigIds);
                throw new InvalidOperationException(
                    $"指定的发件箱连接 {specifiedConfigId} 未登记在当前工作单元（已登记：{enlisted}），" +
                    "事件无法与业务数据写入同一个事务。");
            }

            return specifiedConfigId;
        }

        if (enlistedConfigIds.Count == 1)
        {
            return enlistedConfigIds[0];
        }

        if (enlistedConfigIds.Count == 0)
        {
            var layoutConfigIds = _clientResolver.GetCurrentLayoutConfigIds();
            if (layoutConfigIds.Count == 0)
            {
                throw new InvalidOperationException("无法解析当前数据库布局，事件无法入箱。");
            }

            return layoutConfigIds[0];
        }

        throw new InvalidOperationException(
            $"当前工作单元登记了多个数据库连接（{string.Join("、", enlistedConfigIds)}），无法确定事件应写入哪个库。" +
            "请用 ISqlSugarOutboxConnectionScope.Use 指定承载业务数据的连接，或拆分工作单元。");
    }

    /// <summary>
    /// 在无租户上下文、独立的非事务工作单元中查找租户的投递目标
    /// </summary>
    /// <param name="tenantId">租户标识</param>
    /// <returns>投递目标，未注册目录或不在目录中时为 null</returns>
    private async Task<OutboxDeliveryTarget?> FindTargetAsync(long tenantId)
    {
        if (_targetProvider is null)
        {
            return null;
        }

        using (_currentTenant.Change(null))
        {
            if (_unitOfWorkManager is null)
            {
                return await _targetProvider.FindAsync(tenantId);
            }

            using var unitOfWork = _unitOfWorkManager.Begin(new XiHanUnitOfWorkOptions { IsTransactional = false }, requiresNew: true);
            var target = await _targetProvider.FindAsync(tenantId);
            await unitOfWork.CompleteAsync();

            return target;
        }
    }

    /// <summary>
    /// 确认写入指定连接的事件会被发送循环投递
    /// </summary>
    /// <param name="configId">入箱写入的连接配置标识</param>
    /// <returns>表示异步操作的任务</returns>
    /// <exception cref="InvalidOperationException">写入该连接的事件不会被投递，或当前租户的投递目标已停用</exception>
    private async Task EnsureDeliverableAsync(string configId)
    {
        if (_currentTenant.Id is not { } tenantId || tenantId <= 0)
        {
            return;
        }

        var target = await FindTargetAsync(tenantId);
        if (target is { IsEnabled: false })
        {
            throw new InvalidOperationException(
                $"租户 {tenantId} 的发件箱投递目标已停用：既有事件仍会被送完，但不再接受新事件入箱。");
        }

        IReadOnlyList<string> platformLayout;
        using (_currentTenant.Change(null))
        {
            platformLayout = _clientResolver.GetCurrentLayoutConfigIds();
        }

        if (platformLayout.Contains(configId, StringComparer.Ordinal))
        {
            return;
        }

        if (_targetProvider is null)
        {
            throw new InvalidOperationException(
                $"发件箱不支持未登记的租户独立库：当前租户 {tenantId} 的事件将写入独立于平台的数据库 {configId}，" +
                "未注册 IOutboxDeliveryTargetProvider 时发送循环不会扫描该库。请注册发件箱投递目标目录，或改用共享库的隔离方式。");
        }

        if (target is null)
        {
            throw new InvalidOperationException(
                $"租户 {tenantId} 不在发件箱投递目标目录中，写入其独立库 {configId} 的事件不会被发送循环投递。请先把该租户加入目录。");
        }

        if (!_clientResolver.GetCurrentLayoutConfigIds().Contains(configId, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"连接 {configId} 既不在平台布局中，也不在租户 {tenantId} 的布局中，写入其中的事件不会被发送循环投递。");
        }
    }

    /// <summary>
    /// 领取一批待发送的事件信息
    /// </summary>
    /// <remarks>
    /// 本方法在返回前会把记录标记为已领取，不是纯查询。
    /// 领取超时后记录可被重新领取，超时时长由 <see cref="XiHanSqlSugarEventBoxOptions.ClaimTimeout"/> 配置。
    /// 租户上下文中只作用于租户独立的库，平台布局内的库由宿主上下文负责。
    /// 领取遍历可扫描的全部库，起始库逐次轮换；每个库分到剩余配额按剩余库数均分后的上取整，
    /// 前面的库领不满时余量顺延给后面的库，领满配额的库在第二轮继续分剩余配额；单次领取总量不超过 <c>maxCount</c>。
    /// 某个库不可达时记录日志并跳过。
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

        var configIds = GetScannableConfigIds();
        if (configIds.Count == 0)
        {
            return [];
        }

        var start = (int)((uint)Interlocked.Increment(ref _claimRotation) % (uint)configIds.Count);
        List<string> candidates = [.. configIds.Skip(start), .. configIds.Take(start)];
        var remaining = maxCount;
        var claimed = new List<SysEventOutbox>();

        for (var pass = 0; pass < MaxClaimPasses && remaining > 0 && candidates.Count > 0; pass++)
        {
            var saturated = new List<string>();

            for (var index = 0; index < candidates.Count && remaining > 0; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var configId = candidates[index];
                var databasesLeft = candidates.Count - index;
                var quota = (remaining + databasesLeft - 1) / databasesLeft;

                try
                {
                    var client = _clientResolver.GetClient(configId);
                    var rows = await ClaimFromDatabaseAsync(client, quota, cancellationToken);

                    foreach (var row in rows)
                    {
                        _claimSources[row.BasicId] = new ClaimSource(configId, row.ClaimToken!);
                    }

                    claimed.AddRange(rows);
                    remaining -= rows.Count;

                    if (rows.Count == quota)
                    {
                        saturated.Add(configId);
                    }
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

            candidates = saturated;
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
    /// 本实例领取过的事件只在其来源库、按当时的领取令牌删除，不受当前租户上下文影响；
    /// 令牌已变（记录被其他实例重新领取）时不删除。
    /// 其余标识遍历可扫描的全部库删除；租户上下文中只作用于租户独立的库，平台布局内的库由宿主上下文负责。
    /// 主键全局唯一，没有该记录的库上执行只删除 0 行。
    /// 某个库删除失败时记录日志并跳过该库，不向调用方传播异常；该库上的记录保持可领取状态，
    /// 会在后续轮询中被重新领取并再次投递。
    /// </remarks>
    /// <param name="ids">事件唯一标识符集合</param>
    public async Task DeleteManyAsync(IEnumerable<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
        {
            return;
        }

        var locatedGroups = idList
            .Where(_claimSources.ContainsKey)
            .GroupBy(id => _claimSources[id])
            .ToList();

        foreach (var group in locatedGroups)
        {
            var configId = group.Key.ConfigId;
            var claimToken = group.Key.ClaimToken;
            var groupIds = group.ToList();

            try
            {
                var client = _clientResolver.GetClient(configId);

                await client.Deleteable<SysEventOutbox>()
                    .Where(item => groupIds.Contains(item.BasicId) && item.ClaimToken == claimToken)
                    .ExecuteCommandAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从数据库 {ConfigId} 删除已投递事件失败，已跳过该库。", configId);
            }

            foreach (var id in groupIds)
            {
                _claimSources.Remove(id);
            }
        }

        var unlocatedIds = idList.Where(id => locatedGroups.All(group => !group.Contains(id))).ToList();
        if (unlocatedIds.Count == 0)
        {
            return;
        }

        foreach (var configId in GetScannableConfigIds())
        {
            try
            {
                var client = _clientResolver.GetClient(configId);

                await client.Deleteable<SysEventOutbox>().In(unlocatedIds).ExecuteCommandAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从数据库 {ConfigId} 删除已投递事件失败，已跳过该库。", configId);
            }
        }
    }

    /// <summary>
    /// 统计可扫描的各库中尚未删除的事件数
    /// </summary>
    /// <remarks>
    /// 租户上下文中只作用于租户独立的库，平台布局内的库由宿主上下文负责。
    /// 待发送与已领取但尚未删除的事件都计入。任一库不可达时抛出异常，不返回部分结果。
    /// 多个连接配置标识指向同一物理库时按标识分别计数。
    /// </remarks>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>尚未删除的事件数</returns>
    public async Task<long> GetPendingCountAsync(CancellationToken cancellationToken = default)
    {
        long total = 0;

        foreach (var configId in GetScannableConfigIds().Distinct(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var client = _clientResolver.GetClient(configId);
            total += await client.Queryable<SysEventOutbox>().CountAsync(cancellationToken);
        }

        return total;
    }

    /// <summary>
    /// 获取领取、遍历删除与待送数统计所作用的连接配置标识
    /// </summary>
    /// <returns>当前布局的连接配置标识；处于租户上下文时剔除平台布局内的连接</returns>
    private IReadOnlyList<string> GetScannableConfigIds()
    {
        var configIds = _clientResolver.GetCurrentLayoutConfigIds();
        if (_currentTenant.Id is not > 0)
        {
            return configIds;
        }

        IReadOnlyList<string> platformLayout;
        using (_currentTenant.Change(null))
        {
            platformLayout = _clientResolver.GetCurrentLayoutConfigIds();
        }

        return [.. configIds.Where(configId => !platformLayout.Contains(configId, StringComparer.Ordinal))];
    }

    /// <summary>
    /// 领取来源：记录所在库的连接配置标识与领取令牌
    /// </summary>
    /// <param name="ConfigId">连接配置标识</param>
    /// <param name="ClaimToken">领取令牌</param>
    private readonly record struct ClaimSource(string ConfigId, string ClaimToken);
}
