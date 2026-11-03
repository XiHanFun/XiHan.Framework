// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using XiHan.Framework.Persistence.Conformance.Fixtures;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// 在新增落库之前抢先写入同一标识的行
/// </summary>
/// <remarks>
/// 模拟「仓储核对过标识之后、写入之前，别的事务提交了同一标识」：SQLite 单写者，别的连接这时写不进来，所以借同一连接与事务插入，效果与对方已提交相同——仓储的插入撞上主键约束、在同一事务里核对时能看到这一行。只在第一次保存含该标识的新增时抢一次；插入发生在 SaveChanges 建保存点之前，回到保存点不会撤销它
/// </remarks>
/// <param name="id">要抢的标识</param>
internal sealed class RacingInsertInterceptor(Guid id) : SaveChangesInterceptor
{
    /// <summary>
    /// 是否已经抢过
    /// </summary>
    private int _raced;

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (eventData.Context is { Database.CurrentTransaction: { } transaction } context
            && context.ChangeTracker.Entries<ConformanceOrder>().Any(e => e.State == EntityState.Added && e.Entity.Id == id)
            && Interlocked.Exchange(ref _raced, 1) == 0)
        {
            // 另一个上下文共用这条连接与事务，按同一模型写入，列类型与仓储写入的一致
            await using var rival = new ConformanceDbContext(new DbContextOptionsBuilder<ConformanceDbContext>().UseSqlite(context.Database.GetDbConnection()).Options);
            await rival.Database.UseTransactionAsync(transaction.GetDbTransaction(), cancellationToken).ConfigureAwait(false);
            rival.Add(new ConformanceOrder { Id = id, Name = "rival", ConcurrencyStamp = "rival", CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) });
            await rival.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }
}
