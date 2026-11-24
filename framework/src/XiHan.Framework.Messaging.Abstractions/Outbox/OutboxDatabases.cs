// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Outbox;

/// <summary>
/// 发件箱行所在的库：默认库与租户分片库
/// </summary>
/// <remarks>
/// 投递循环、健康探针与死信重投都不在任何调用里，各自要在每个库上开一个工作单元。「有哪些库」与「怎样在一个库上开工作单元」收在这里一份，三处覆盖的库因此总是同一批（ADR-0211、ADR-0272）。装配了租户分片（容器里有租户目录与分片解析器）且没有关掉 <see cref="OutboxOptions.IncludeShards"/> 时，默认库之外逐个纳入分片库；分片清单每 <see cref="OutboxOptions.ShardRefreshInterval"/> 重读一次，重读失败记日志、沿用上一次的清单
/// </remarks>
/// <param name="services">根服务提供者，每个工作单元在它上面开新作用域</param>
/// <param name="options">发件箱选项：是否纳入分片库与清单的重读间隔</param>
/// <param name="timeProvider">时间源，判定清单是否该重读</param>
internal sealed partial class OutboxDatabases(IServiceProvider services, OutboxOptions options, TimeProvider timeProvider)
{
    /// <summary>
    /// 根服务提供者
    /// </summary>
    private readonly IServiceProvider _services = services ?? throw new ArgumentNullException(nameof(services));

    /// <summary>
    /// 发件箱选项
    /// </summary>
    private readonly OutboxOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// 时间源
    /// </summary>
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>
    /// 上一次读出的分片清单
    /// </summary>
    /// <remarks>
    /// 还没读过为空。整个替换，读的一方拿到的总是同一次读取的清单与时刻；几个使用方同时重读时各读各的、后写的留下，清单内容相同
    /// </remarks>
    private volatile ShardCatalog? _shardCatalog;

    /// <summary>
    /// 要处理的库，默认库在前
    /// </summary>
    /// <remarks>
    /// 默认库以 <see langword="null"/> 表示；没有装配分片、或关掉了 <see cref="OutboxOptions.IncludeShards"/> 时只有它。清单重读失败经 <paramref name="logger"/> 记警告、沿用上一次的清单；第一次就失败时只有默认库
    /// </remarks>
    /// <param name="logger">使用方自己的日志</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async ValueTask<IReadOnlyList<TenantShard?>> ListAsync(ILogger logger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(logger);
        return [null, .. await ShardsAsync(logger, cancellationToken).ConfigureAwait(false)];
    }

    /// <summary>
    /// 在一个库的新作用域与工作单元里读写发件箱仓储
    /// </summary>
    /// <remarks>
    /// <paramref name="shard"/> 为空是默认库；否则在新作用域里先绑定这个分片、再取工作单元与仓储，工作单元的租户是分片的租户。开作用域、提交与回滚走 <see cref="UnitOfWorkScope"/>：异常回滚后原样抛出，回滚与释放作用域自身的失败只经 <paramref name="logger"/> 记警告（ADR-0259）
    /// </remarks>
    /// <param name="shard">分片；默认库为 <see langword="null"/></param>
    /// <param name="readOnly">只读：不开事务，配了从库时读从库</param>
    /// <param name="body">在活动的工作单元里执行的动作，参数是这个库的发件箱仓储</param>
    /// <param name="logger">使用方自己的日志</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>动作的结果，或提交返回的失败</returns>
    public ValueTask<Result<T>> RunAsync<T>(TenantShard? shard, bool readOnly, Func<IRepository<OutboxMessage, Guid>, CancellationToken, ValueTask<Result<T>>> body, ILogger logger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(logger);
        return UnitOfWorkScope.RunAsync(
            _services,
            (scope, _) =>
            {
                if (shard is not null)
                {
                    // 先绑定再取工作单元：持久化客户端在首次取用时按绑定建连接，建出之后不能再绑定
                    scope.GetRequiredService<TenantShardScope>().Bind(shard);
                }

                return ValueTask.FromResult(Result.Success());
            },
            new UnitOfWorkOptions { IsReadOnly = readOnly, TenantId = shard?.TenantId },
            (scope, ct) => body(scope.GetRequiredService<IRepository<OutboxMessage, Guid>>(), ct),
            logger,
            cancellationToken);
    }

    /// <summary>
    /// 当前的分片库
    /// </summary>
    /// <remarks>
    /// 没有装配分片（容器里没有租户目录或分片解析器）或关掉了 <see cref="OutboxOptions.IncludeShards"/> 时为空
    /// </remarks>
    private async ValueTask<IReadOnlyList<TenantShard>> ShardsAsync(ILogger logger, CancellationToken cancellationToken)
    {
        if (!_options.IncludeShards || _services.GetService<ITenantShardResolver>() is not { } resolver || _services.GetService<ITenantStore>() is not { } tenants)
        {
            return [];
        }

        var now = _timeProvider.GetUtcNow();
        var catalog = _shardCatalog;
        if (catalog is not null && now - catalog.ReadAt < _options.ShardRefreshInterval)
        {
            return catalog.Shards;
        }

        var read = await ReadShardsAsync(tenants, resolver, cancellationToken).ConfigureAwait(false);
        if (read.TryGetValue(out var shards))
        {
            _shardCatalog = new ShardCatalog(shards, now);
            return shards;
        }

        // 不记读取时刻：下一次再读。第一次就失败时只有默认库
        LogShardsFailed(logger, read.Error!.Code);
        return catalog?.Shards ?? [];
    }

    /// <summary>
    /// 从租户目录读出全部分片库
    /// </summary>
    /// <remarks>
    /// 逐页读完全部租户，含停用的：停用之前写进发件箱的消息照常发出。共库的租户（解析不出分片）在默认库；按连接串与数据库种类去重，每个分片库取它的第一个租户
    /// </remarks>
    private static async ValueTask<Result<IReadOnlyList<TenantShard>>> ReadShardsAsync(ITenantStore tenants, ITenantShardResolver resolver, CancellationToken cancellationToken)
    {
        var shards = new List<TenantShard>();
        var seen = new HashSet<(string ConnectionString, string? Provider)>();
        var query = new TenantQuery { PageSize = TenantQuery.MaxPageSize };
        while (true)
        {
            var page = await tenants.ListAsync(query, cancellationToken).ConfigureAwait(false);
            if (!page.TryGetValue(out var listed))
            {
                return page.Error!;
            }

            foreach (var tenant in listed.Items)
            {
                var resolved = await resolver.ResolveAsync(tenant.Id, cancellationToken).ConfigureAwait(false);
                if (!resolved.TryGetValue(out var found))
                {
                    return resolved.Error!;
                }

                // 同一个库的多个租户只算一次：发件箱行不分租户，在这个库上读到的就是它全部的行
                if (found.TryGetValue(out var shard) && seen.Add((shard.ConnectionString, shard.Provider)))
                {
                    shards.Add(shard);
                }
            }

            if (listed.ContinuationToken is null)
            {
                return Result.Success<IReadOnlyList<TenantShard>>(shards);
            }

            query = query with { ContinuationToken = listed.ContinuationToken };
        }
    }

    /// <summary>
    /// 记录分片清单读取失败
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Reading the tenant shards of the outbox failed with {ErrorCode}; the previous shard list is kept and the next round reads again.")]
    private static partial void LogShardsFailed(ILogger logger, string errorCode);

    /// <summary>
    /// 一次读出的分片清单与读取时刻
    /// </summary>
    private sealed record ShardCatalog(IReadOnlyList<TenantShard> Shards, DateTimeOffset ReadAt);
}
