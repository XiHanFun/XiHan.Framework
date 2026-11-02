// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.EntityFrameworkCore;

/// <summary>
/// 租户分片库的时间列核对（单例）。
/// </summary>
/// <remarks>
/// 分片库在调用时才解析、启动时枚举不到，所以在首次绑定时核对：工作单元开始时 DbContext 建在分片上、这个分片库还没有核对通过，就按 <see cref="SqliteTimestampCheck"/> 核对一次（RFC-0005「EF Core 在 SQLite 上的时间列核对」，ADR-0097）。
/// 结果按分片连接串记下：核对通过的之后不再查；同一分片的并发首绑排队、只有一个去查；不符、库文件还不存在、核对抛出异常都不记下，下次绑定重查——修好库即恢复，不用重启。
/// 记录一个分片库一条（一个标志与一个不占系统句柄的信号量），条数随应用的分片库数，与分片目录同量级。
/// </remarks>
internal sealed class ShardTimestampChecks
{
    /// <summary>
    /// 每个分片库的核对状态，键是分片连接串。
    /// </summary>
    /// <remarks>
    /// 连接串是机密：只作内存里的键，不进日志、不进消息。
    /// </remarks>
    private readonly ConcurrentDictionary<string, ShardState> _shards = new(StringComparer.Ordinal);

    /// <summary>
    /// 核对 <paramref name="context"/> 所在的分片库，不符即抛出。
    /// </summary>
    /// <remarks>
    /// <paramref name="context"/> 建在 <paramref name="shard"/> 上；<paramref name="placedHere"/> 判定实体是否落在它上面。不符时抛出携带 <see cref="PersistenceErrors.StorageFormatMismatch"/> 的 <see cref="FrameworkException"/>，消息带分片的租户与不符的列、不带连接串。
    /// </remarks>
    public async ValueTask VerifyAsync(DbContext context, BindingValue shard, Func<Type, bool> placedHere, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(shard);
        // 不是 SQLite 的分片没有可核对的：不建记录，分片再多也不占内存
        if (!SqliteTimestampCheck.IsSqlite(context) || shard.Value is not { } connectionString)
        {
            return;
        }

        var state = _shards.GetOrAdd(connectionString, static _ => new ShardState());
        // 快路径：核对通过的分片之后每次开工作单元只读这个标志，不排队、不碰库
        if (state.Verified)
        {
            return;
        }

        // 同一分片的首绑排队：第一个去查，其余等它；等到之后再看一次标志，查过了就不再各查一遍。
        // 每个等待者用自己的令牌等、出队后用自己的 DbContext 查：前一个被取消或抛出时，下一个接着查，不受它的令牌牵连
        await state.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (state.Verified)
            {
                return;
            }

            var mismatches = await SqliteTimestampCheck.FindMismatchesAsync(context, placedHere, cancellationToken).ConfigureAwait(false);
            // 库文件还不存在：没有可核对的，也不能记成已核对——之后放进来的可能是旧库，下次绑定再查
            if (mismatches is null)
            {
                return;
            }

            if (mismatches.Count > 0)
            {
                // 不符不记下：每次绑定都重查、都失败，修好库（按换算式重建表）后下一次绑定即通过，不用重启
                shard.Properties.TryGetValue(TenantShardKeys.TenantProperty, out var tenant);
                Result.Failure(Error.Validation(PersistenceErrors.StorageFormatMismatch, PersistenceMessages.ShardTimestampFormatMismatch, MessageArguments.Of(("tenant", tenant ?? string.Empty), ("columns", string.Join(", ", mismatches))))).ThrowIfFailure();
            }

            state.Verified = true;
        }
        finally
        {
            state.Gate.Release();
        }
    }

    /// <summary>
    /// 一个分片库的核对状态。
    /// </summary>
    private sealed class ShardState
    {
        /// <summary>
        /// 是否已核对通过。
        /// </summary>
        /// <remarks>
        /// 只由持有 <see cref="Gate"/> 的一方从 false 改成 true，之后不再变；快路径不加锁读它。
        /// </remarks>
        private volatile bool _verified;

        /// <summary>
        /// 首绑排队的门。
        /// </summary>
        /// <remarks>
        /// 只用异步等待，不取等待句柄，不需要释放。
        /// </remarks>
        public SemaphoreSlim Gate { get; } = new(1, 1);

        /// <summary>
        /// 是否已核对通过。
        /// </summary>
        public bool Verified
        {
            get => _verified;
            set => _verified = value;
        }
    }
}
