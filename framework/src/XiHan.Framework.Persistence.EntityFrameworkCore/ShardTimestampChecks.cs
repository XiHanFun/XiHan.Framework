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
/// 租户分片库的时间列核对（单例）
/// </summary>
/// <remarks>
/// 分片库在调用时才解析、启动时枚举不到，所以在首次绑定时核对：工作单元开始时 DbContext 建在分片上、这个分片库还没有核对通过，就按 <see cref="SqliteTimestampCheck"/> 核对一次（RFC-0005「EF Core 在 SQLite 上的时间列核对」，ADR-0097）
/// 核对通过的按分片连接串记下，之后不再查；记录最多 <see cref="Capacity"/> 条（<see cref="EntityFrameworkCorePersistenceOptions.VerifiedShardCapacity"/>），记满时淘汰最早记下的，被淘汰的分片下次绑定时重新核对一次。同一分片的并发首绑排队、只有一个去查，排队门在没人用时即移除，条数不超过同时在核对的分片数
/// 不符、库文件还不存在、核对抛出异常都不记下，下次绑定重查——修好库即恢复，不用重启
/// </remarks>
internal sealed class ShardTimestampChecks
{
    /// <summary>
    /// 核对通过的分片，键是分片连接串
    /// </summary>
    /// <remarks>
    /// 读不加锁（每次开工作单元的快路径），增删只在 <see cref="_gate"/> 里与 <see cref="_order"/> 一起改。连接串是机密：只作内存里的键，不进日志、不进消息
    /// </remarks>
    private readonly ConcurrentDictionary<string, byte> _verified = new(StringComparer.Ordinal);

    /// <summary>
    /// 核对通过的先后，最早的在队首
    /// </summary>
    private readonly Queue<string> _order = new();

    /// <summary>
    /// 正在核对的分片的排队门，键是分片连接串
    /// </summary>
    private readonly Dictionary<string, Pending> _pending = new(StringComparer.Ordinal);

    /// <summary>
    /// 保护 <see cref="_order"/>、<see cref="_pending"/> 与 <see cref="_verified"/> 的增删
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="capacity">核对通过的分片最多记多少个</param>
    public ShardTimestampChecks(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        Capacity = capacity;
    }

    /// <summary>
    /// 核对通过的分片最多记多少个
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// 核对 <paramref name="context"/> 所在的分片库，不符即抛出
    /// </summary>
    /// <remarks>
    /// <paramref name="context"/> 建在 <paramref name="shard"/> 上；<paramref name="placedHere"/> 判定实体是否落在它上面。不符时抛出携带 <see cref="PersistenceErrors.StorageFormatMismatch"/> 的 <see cref="FrameworkException"/>，消息带分片的租户与不符的列、不带连接串
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

        // 快路径：核对通过的分片之后每次开工作单元只查一次字典，不加锁、不排队、不碰库
        if (_verified.ContainsKey(connectionString))
        {
            return;
        }

        Pending pending;
        lock (_gate)
        {
            // 锁里再看一次：别的首绑可能刚记下。排队门按分片取同一个，登记一个使用者，最后一个使用者离开时移除
            if (_verified.ContainsKey(connectionString))
            {
                return;
            }

            if (!_pending.TryGetValue(connectionString, out var existing))
            {
                existing = new Pending();
                _pending[connectionString] = existing;
            }

            pending = existing;
            pending.Users++;
        }

        try
        {
            // 同一分片的首绑排队：第一个去查，其余等它；等到之后再看一次记录，查过了就不再各查一遍
            // 每个等待者用自己的令牌等、出队后用自己的 DbContext 查：前一个被取消或抛出时，下一个接着查，不受它的令牌牵连
            await pending.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_verified.ContainsKey(connectionString))
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

                Remember(connectionString);
            }
            finally
            {
                pending.Gate.Release();
            }
        }
        finally
        {
            lock (_gate)
            {
                // 最后一个使用者离开：没有人再拿着这个门（新来的首绑在锁里会建新的），移除并释放，排队门不随分片数增长
                if (--pending.Users == 0)
                {
                    _pending.Remove(connectionString);
                    pending.Gate.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// 记下核对通过的分片，记满时淘汰最早记下的
    /// </summary>
    /// <remarks>
    /// 只由持有这个分片排队门的一方调用，同一分片不会同时记两次。被淘汰的分片只是下次绑定时重新核对一次，语义不变
    /// </remarks>
    private void Remember(string connectionString)
    {
        lock (_gate)
        {
            if (!_verified.TryAdd(connectionString, 0))
            {
                return;
            }

            _order.Enqueue(connectionString);
            // 先进先出淘汰：快路径不记访问先后，命中不付任何写的代价；热的分片被淘汰也只多核对一次
            while (_order.Count > Capacity)
            {
                _verified.TryRemove(_order.Dequeue(), out _);
            }
        }
    }

    /// <summary>
    /// 一个分片的排队门与它的使用者数
    /// </summary>
    private sealed class Pending
    {
        /// <summary>
        /// 首绑排队的门
        /// </summary>
        public SemaphoreSlim Gate { get; } = new(1, 1);

        /// <summary>
        /// 正在用这个门的首绑数
        /// </summary>
        /// <remarks>
        /// 只在 <see cref="_gate"/> 里改
        /// </remarks>
        public int Users { get; set; }
    }
}
