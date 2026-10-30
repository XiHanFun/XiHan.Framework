// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Scheduling.Abstractions;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Leadership;
using XiHan.Framework.Scheduling.Database.Entities;
using XiHan.Framework.Scheduling.Database.Leadership;
using XiHan.Framework.Scheduling.Database.Storage;

namespace XiHan.Framework.Scheduling.Database.Tests;

/// <summary>
/// 租约行。
/// </summary>
/// <remarks>
/// 被别的节点占着、令牌不对、行没了都是成功的无值，不是失败；租约被接管后续约是失去租约，不是存储不可达。
/// </remarks>
public sealed class LeaseRowsTests : IAsyncLifetime
{
    /// <summary>
    /// 本用例的数据库与宿主。
    /// </summary>
    private readonly SqliteDatabase _database = new();

    /// <inheritdoc />
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task HeldWrongTokenOrMissingRow_AreEmptySuccesses()
    {
        var host = await _database.StartHostAsync();
        var rows = new LeaseRows(host.Services, host.App, TimeProvider.System);
        var ttl = TimeSpan.FromMinutes(1);

        Assert.True((await rows.TryAcquireAsync("lease.rows", "node-a", "token-a", ttl, CancellationToken.None)).ValueOrThrow().HasValue);

        // 被别人占着：成功且无值（之前构造空值的成功结果会抛 ArgumentNullException，在管道里记成未处理异常）
        var held = await rows.TryAcquireAsync("lease.rows", "node-b", "token-b", ttl, CancellationToken.None);
        Assert.True(held.IsSuccess, held.Error?.Code);
        Assert.False(held.ValueOrThrow().HasValue);

        // 令牌不对：续约成功且无值；令牌对：拿到新的到期时刻
        Assert.False((await rows.RenewAsync("lease.rows", "token-b", ttl, CancellationToken.None)).ValueOrThrow().HasValue);
        Assert.True((await rows.RenewAsync("lease.rows", "token-a", ttl, CancellationToken.None)).ValueOrThrow().HasValue);

        // 行没了：续约成功且无值
        (await rows.ReleaseAsync("lease.rows", "token-a", CancellationToken.None)).ThrowIfFailure();
        Assert.False((await rows.RenewAsync("lease.rows", "token-a", ttl, CancellationToken.None)).ValueOrThrow().HasValue);
    }

    [Fact]
    public async Task Renew_AfterAnotherNodeTookOver_LosesTheLease()
    {
        var host = await _database.StartHostAsync();
        var election = host.Services.GetRequiredService<ILeaderElection>();
        var resource = new JobName("lease.takeover");
        var lease = (await election.TryAcquireAsync(resource, TimeSpan.FromMinutes(1), CancellationToken.None)).ValueOrThrow();

        // 别的节点接管：行换了持有者与令牌
        (await new StoreInvocation(host.Services, host.App).RunAsync(async ctx =>
        {
            var repository = StoreInvocation.Repository<SchedulerLeaseEntity>(ctx);
            var row = (await repository.FindAsync(resource.Value, ctx.CancellationToken)).ValueOrThrow();
            row.Holder = "node-b";
            row.Token = "token-b";
            return await repository.UpdateAsync(row, ctx.CancellationToken);
        }, CancellationToken.None)).ThrowIfFailure();

        // 续约是失去租约（被占用），不是存储不可达：之后不再当自己是领导者
        Assert.Equal(SchedulingErrors.LeaseHeld, (await lease.RenewAsync(CancellationToken.None)).Error?.Code);
        Assert.False(lease.IsHeld);
    }
}
