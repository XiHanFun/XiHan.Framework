// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.MultiTenancy.InMemory;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// SQLite 时间列核对在租户分片上的首次绑定。
/// </summary>
/// <remarks>
/// RFC-0005「EF Core 在 SQLite 上的时间列核对」，ADR-0097。分片库启动时枚举不到，工作单元开始时 DbContext 建在分片上、这个分片库还没有核对通过就核对一次：不符即那次工作单元开不起来（<see cref="PersistenceErrors.StorageFormatMismatch"/>，经管道是 <c>KERNEL_UNEXPECTED_001</c>），别的租户照常；核对通过的分片只查一次，并发首绑也只查一次；不符与库还不存在不记下，修好或建出之后下一次绑定再查。
/// 处理器不碰库：分片上的连接打开只来自核对，打开次数就是核对次数（<see cref="ShardOpenCounter"/>）。
/// </remarks>
public sealed class EntityFrameworkCoreShardTimestampCheckTests : IDisposable
{
    /// <summary>
    /// 有分片的租户。
    /// </summary>
    private const string Acme = "acme";

    /// <summary>
    /// 没有分片的租户。
    /// </summary>
    private const string Beta = "beta";

    /// <summary>
    /// 应用默认库。
    /// </summary>
    private readonly SqliteDatabase _default = new();

    /// <summary>
    /// 租户 <see cref="Acme"/> 的分片库。
    /// </summary>
    private readonly SqliteDatabase _shard = new();

    /// <summary>
    /// 分片库里的两行：不同偏移、带小数秒。
    /// </summary>
    private readonly TimedRow[] _rows =
    [
        new() { Id = Guid.CreateVersion7(), At = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.FromHours(8)), Due = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero) },
        new() { Id = Guid.CreateVersion7(), At = new DateTimeOffset(2026, 10, 31, 20, 0, 0, TimeSpan.FromHours(-5)).AddTicks(1234567) },
    ];

    /// <summary>
    /// 删除两个临时数据库文件。
    /// </summary>
    public void Dispose()
    {
        _default.Dispose();
        _shard.Dispose();
    }

    [Fact]
    public async Task LegacyShard_FailsAtEveryBinding_OtherTenantsServe_AndAFixedShardServesWithoutRestart()
    {
        // 分片库还是旧库：列是 TEXT。启动只核对默认库（新建的，INTEGER），分片启动时枚举不到
        await EntityFrameworkCoreSqliteTimestampCheckTests.CreateLegacyAsync(_shard, _rows);
        var counter = new ShardOpenCounter(_shard.Path);
        await using var host = await StartAsync(counter);

        var failed = await InvokeAsync(host, Acme);

        // 工作单元在 Transaction 阶段开不起来：Observe 阶段转为 KERNEL_UNEXPECTED_001，原因是存储形式不符，消息带租户与不符的列、不带连接串
        Assert.Equal(KernelErrors.Unexpected, failed.Error?.Code);
        var cause = failed.Error!.Inner;
        Assert.Equal(PersistenceErrors.StorageFormatMismatch, cause?.Code);
        Assert.Contains(Acme, cause!.Message, StringComparison.Ordinal);
        Assert.Contains("TimedRow.At (TEXT)", cause.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(_shard.Path, cause.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, counter.Opens);

        // 别的租户（默认库）照常；不符不记下，同一分片的下一次绑定重查、仍失败
        (await InvokeAsync(host, Beta)).ThrowIfFailure();
        Assert.Equal(PersistenceErrors.StorageFormatMismatch, (await InvokeAsync(host, Acme)).Error?.Inner?.Code);
        Assert.Equal(2, counter.Opens);

        // 按 ADR-0091 第三节修好分片库：下一次绑定即通过，不用重启，读回的是同一时刻
        EntityFrameworkCoreSqliteTimestampCheckTests.Execute(_shard, EntityFrameworkCoreSqliteTimestampCheckTests.Rebuild(EntityFrameworkCoreSqliteTimestampCheckTests.UtcTicksFormula));
        IReadOnlyList<TimedRow> read = [];
        (await InvokeAsync(host, Acme, async context => read = await context.Services.GetRequiredService<IRepository<TimedRow, Guid>>().ListAsync(EntityQuery.All<TimedRow>(), context.CancellationToken))).ThrowIfFailure();
        Assert.Equal(_rows.Select(static row => (row.Id, row.At.UtcTicks)).OrderBy(static row => row.UtcTicks), read.Select(static row => (row.Id, row.At.UtcTicks)).OrderBy(static row => row.UtcTicks));

        // 通过之后记下：再绑定不再核对
        var opens = counter.Opens;
        (await InvokeAsync(host, Acme)).ThrowIfFailure();
        Assert.Equal(opens, counter.Opens);
    }

    [Fact]
    public async Task ConformingShard_IsCheckedOnce_EvenWhenItsFirstBindingsRace()
    {
        await using (var context = new TimedDbContext(new DbContextOptionsBuilder<TimedDbContext>().UseSqlite(_shard.ConnectionString).Options))
        {
            await context.Database.EnsureCreatedAsync();
            context.AddRange(_rows);
            await context.SaveChangesAsync();
        }

        // 第一次核对打开分片之后挂起：它持有这个分片的排队门，其余首绑都在门外等
        var counter = new ShardOpenCounter(_shard.Path) { Hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        var built = 0;
        var allBuilt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        const int Racers = 8;
        await using var host = await StartAsync(counter, onShardContext: () =>
        {
            // DbContext 建在分片上之后紧接着就是核对：八个都建出，说明八次首绑都到了核对这一步
            if (Interlocked.Increment(ref built) == Racers)
            {
                allBuilt.SetResult();
            }
        });

        var first = InvokeAsync(host, Acme);
        await counter.Entered.Task;
        var others = Enumerable.Range(1, Racers - 1).Select(_ => InvokeAsync(host, Acme)).ToList();
        await allBuilt.Task;
        counter.Hold.SetResult();

        Assert.All(await Task.WhenAll([first, .. others]), static result => result.ThrowIfFailure());
        Assert.Equal(1, counter.Opens);

        // 之后的绑定只看记录，不再碰库
        for (var i = 0; i < 3; i++)
        {
            (await InvokeAsync(host, Acme)).ThrowIfFailure();
        }

        Assert.Equal(1, counter.Opens);
    }

    [Fact]
    public async Task MissingShardDatabase_IsNotCreated_AndIsCheckedOnceItExists()
    {
        // 分片库文件还不存在：没有可核对的，核对不打开连接（SQLite 打开不存在的文件会建出空库），也不记成已核对
        var counter = new ShardOpenCounter(_shard.Path);
        await using var host = await StartAsync(counter);

        (await InvokeAsync(host, Acme)).ThrowIfFailure();

        Assert.False(File.Exists(_shard.Path));
        Assert.Equal(0, counter.Opens);

        // 之后放进来的是旧库：下一次绑定照样查出来
        await EntityFrameworkCoreSqliteTimestampCheckTests.CreateLegacyAsync(_shard, _rows);
        Assert.Equal(PersistenceErrors.StorageFormatMismatch, (await InvokeAsync(host, Acme)).Error?.Inner?.Code);
    }

    /// <summary>
    /// 启动以 <see cref="TimedDbContext"/>、内存多租户与 SQLite 装配的宿主。
    /// </summary>
    /// <remarks>
    /// 默认库开着 <c>EnsureCreated</c>（新建，INTEGER 列）；租户 <see cref="Acme"/> 的分片指向 <see cref="_shard"/>，<see cref="Beta"/> 没有分片。<paramref name="onShardContext"/> 在每次按分片建 DbContext 选项时调用。
    /// </remarks>
    private Task<TestHost> StartAsync(ShardOpenCounter counter, Action? onShardContext = null)
    {
        var shard = _shard.ConnectionString;
        return TestHost.StartAsync(
            builder => builder.UseFeature<InMemoryMultiTenancyFeature>().UseFeature(new EntityFrameworkCorePersistenceFeature<TimedDbContext>((binding, options) =>
            {
                var connectionString = binding.ValueOrThrow().Value;
                if (string.Equals(connectionString, shard, StringComparison.Ordinal))
                {
                    onShardContext?.Invoke();
                }

                options.UseSqlite(connectionString).AddInterceptors(counter);
            })),
            [
                .. _default.Configuration,
                new("XiHan:MultiTenancy:Tenants:" + Acme + ":Properties:" + TenantShardKeys.ConnectionString, shard),
                new("XiHan:MultiTenancy:Tenants:" + Beta + ":DisplayName", "Beta"),
            ],
            static services =>
            {
                services.AddSingleton(TimeProvider.System);
                services.AddRepository<TimedRow, Guid>();
            });
    }

    /// <summary>
    /// 以给定租户（经 Baggage 自报）走完整管道。
    /// </summary>
    /// <remarks>
    /// 没给 <paramref name="action"/> 时处理器不碰库，直接成功。
    /// </remarks>
    private static async Task<Result> InvokeAsync(TestHost host, string tenant, Func<InvocationContext, Task>? action = null)
        => await host.InvokeAsync(
            async context =>
            {
                if (action is not null)
                {
                    await action(context);
                }

                context.SetOutcome(Result.Success());
            },
            baggage: new Dictionary<string, string> { [BaggageKeys.Tenant] = tenant });
}
