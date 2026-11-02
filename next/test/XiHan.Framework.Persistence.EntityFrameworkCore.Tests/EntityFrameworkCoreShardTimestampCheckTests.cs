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
/// 核对通过的记录有上限（<see cref="EntityFrameworkCorePersistenceOptions.VerifiedShardCapacity"/>）：记满淘汰最早记下的，被淘汰的分片下次绑定重新核对一次，并发再绑同样只查一次。
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
    /// 另一个有分片的租户。
    /// </summary>
    private const string Gamma = "gamma";

    /// <summary>
    /// 并发首绑的个数。
    /// </summary>
    private const int Racers = 8;

    /// <summary>
    /// 核对通过的分片记录上限的配置键。
    /// </summary>
    private const string VerifiedShardCapacityKey = $"{EntityFrameworkCorePersistenceOptions.ConfigurationSection}:VerifiedShardCapacity";

    /// <summary>
    /// 应用默认库。
    /// </summary>
    private readonly SqliteDatabase _default = new();

    /// <summary>
    /// 租户 <see cref="Acme"/> 的分片库。
    /// </summary>
    private readonly SqliteDatabase _shard = new();

    /// <summary>
    /// 租户 <see cref="Gamma"/> 的分片库。
    /// </summary>
    private readonly SqliteDatabase _other = new();

    /// <summary>
    /// 按分片建 DbContext 的信号。
    /// </summary>
    /// <remarks>
    /// 并发用例据此知道首绑都已走到核对这一步。
    /// </remarks>
    private readonly BuildSignal _builds = new();

    /// <summary>
    /// 分片库里的两行：不同偏移、带小数秒。
    /// </summary>
    private readonly TimedRow[] _rows =
    [
        new() { Id = Guid.CreateVersion7(), At = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.FromHours(8)), Due = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero) },
        new() { Id = Guid.CreateVersion7(), At = new DateTimeOffset(2026, 10, 31, 20, 0, 0, TimeSpan.FromHours(-5)).AddTicks(1234567) },
    ];

    /// <summary>
    /// 删除三个临时数据库文件。
    /// </summary>
    public void Dispose()
    {
        _default.Dispose();
        _shard.Dispose();
        _other.Dispose();
    }

    [Fact]
    public async Task LegacyShard_FailsAtEveryBinding_OtherTenantsServe_AndAFixedShardServesWithoutRestart()
    {
        // 分片库还是旧库：列是 TEXT。启动只核对默认库（新建的，INTEGER），分片启动时枚举不到
        await EntityFrameworkCoreSqliteTimestampCheckTests.CreateLegacyAsync(_shard, _rows);
        var counter = new ShardOpenCounter(_shard.Path);
        await using var host = await StartAsync([counter]);

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
        await CreateConformingAsync(_shard);
        var counter = new ShardOpenCounter(_shard.Path);
        await using var host = await StartAsync([counter]);

        Assert.All(await RaceAsync(host, Acme, _shard, counter), static result => result.ThrowIfFailure());
        Assert.Equal(1, counter.Opens);

        // 之后的绑定只看记录，不再碰库
        for (var i = 0; i < 3; i++)
        {
            (await InvokeAsync(host, Acme)).ThrowIfFailure();
        }

        Assert.Equal(1, counter.Opens);
    }

    [Fact]
    public async Task VerifiedShards_AreCapped_AnEvictedShardIsCheckedAgainOnce_AndRacingRebindsCheckOnce()
    {
        // 上限 1：记下一个分片就淘汰前一个
        await CreateConformingAsync(_shard);
        await CreateConformingAsync(_other);
        var acme = new ShardOpenCounter(_shard.Path);
        var gamma = new ShardOpenCounter(_other.Path);
        await using var host = await StartAsync([acme, gamma], [new(VerifiedShardCapacityKey, "1")]);

        (await InvokeAsync(host, Acme)).ThrowIfFailure();
        (await InvokeAsync(host, Acme)).ThrowIfFailure();
        Assert.Equal(1, acme.Opens);

        // 记满之后照常工作：gamma 核对一次、记下，acme 被淘汰；没有分片的租户不受影响
        (await InvokeAsync(host, Gamma)).ThrowIfFailure();
        (await InvokeAsync(host, Gamma)).ThrowIfFailure();
        (await InvokeAsync(host, Beta)).ThrowIfFailure();
        Assert.Equal((1, 1), (acme.Opens, gamma.Opens));

        // 被淘汰的分片再次绑定：重新核对一次，之后又只看记录（这次淘汰了 gamma）
        for (var i = 0; i < 3; i++)
        {
            (await InvokeAsync(host, Acme)).ThrowIfFailure();
        }

        Assert.Equal((2, 1), (acme.Opens, gamma.Opens));

        // 被淘汰的分片上并发再绑：只有一个去核对，之后又只看记录
        Assert.All(await RaceAsync(host, Gamma, _other, gamma), static result => result.ThrowIfFailure());
        (await InvokeAsync(host, Gamma)).ThrowIfFailure();
        Assert.Equal((2, 2), (acme.Opens, gamma.Opens));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1e3")]
    [InlineData(" 8")]
    [InlineData("many")]
    public async Task VerifiedShardCapacity_InvalidValues_FailTheStartup(string value)
    {
        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => StartAsync([], [new(VerifiedShardCapacityKey, value)]));

        // 不按缺省处理：失败指出配置键
        Assert.Contains(VerifiedShardCapacityKey, thrown.ToString(), StringComparison.Ordinal);
        Assert.Throws<ArgumentOutOfRangeException>(static () => new EntityFrameworkCorePersistenceOptions { VerifiedShardCapacity = 0 });
    }

    [Fact]
    public async Task MissingShardDatabase_IsNotCreated_AndIsCheckedOnceItExists()
    {
        // 分片库文件还不存在：没有可核对的，核对不打开连接（SQLite 打开不存在的文件会建出空库），也不记成已核对
        var counter = new ShardOpenCounter(_shard.Path);
        await using var host = await StartAsync([counter]);

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
    /// 默认库开着 <c>EnsureCreated</c>（新建，INTEGER 列）；租户 <see cref="Acme"/> 的分片指向 <see cref="_shard"/>、<see cref="Gamma"/> 的指向 <see cref="_other"/>，<see cref="Beta"/> 没有分片。<paramref name="counters"/> 挂到每个上下文上，<paramref name="settings"/> 追加配置；每次按分片建 DbContext 选项时报给 <see cref="_builds"/>。
    /// </remarks>
    private Task<TestHost> StartAsync(IReadOnlyList<ShardOpenCounter> counters, IEnumerable<KeyValuePair<string, string?>>? settings = null)
    {
        string[] shards = [_shard.ConnectionString, _other.ConnectionString];
        var builds = _builds;
        return TestHost.StartAsync(
            builder => builder.UseFeature<InMemoryMultiTenancyFeature>().UseFeature(new EntityFrameworkCorePersistenceFeature<TimedDbContext>((binding, options) =>
            {
                var connectionString = binding.ValueOrThrow().Value ?? throw new InvalidOperationException("The connection binding has no value.");
                if (shards.Contains(connectionString, StringComparer.Ordinal))
                {
                    builds.Built(connectionString);
                }

                options.UseSqlite(connectionString).AddInterceptors(counters);
            })),
            [
                .. _default.Configuration,
                new("XiHan:MultiTenancy:Tenants:" + Acme + ":Properties:" + TenantShardKeys.ConnectionString, _shard.ConnectionString),
                new("XiHan:MultiTenancy:Tenants:" + Beta + ":DisplayName", "Beta"),
                new("XiHan:MultiTenancy:Tenants:" + Gamma + ":Properties:" + TenantShardKeys.ConnectionString, _other.ConnectionString),
                .. settings ?? [],
            ],
            static services =>
            {
                services.AddSingleton(TimeProvider.System);
                services.AddRepository<TimedRow, Guid>();
            });
    }

    /// <summary>
    /// 在 <paramref name="database"/> 建出合格的分片库并写入两行。
    /// </summary>
    private async Task CreateConformingAsync(SqliteDatabase database)
    {
        await using var context = new TimedDbContext(new DbContextOptionsBuilder<TimedDbContext>().UseSqlite(database.ConnectionString).Options);
        await context.Database.EnsureCreatedAsync();
        context.AddRange(_rows.Select(static row => new TimedRow { Id = row.Id, At = row.At, Due = row.Due }));
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// 让 <see cref="Racers"/> 个首绑同时到达同一分片。
    /// </summary>
    /// <remarks>
    /// 第一个核对打开连接之后挂起（它持有这个分片的排队门），等全部首绑都建出 DbContext——建出之后紧接着就是核对——才放行。返回每个调用的结果。
    /// </remarks>
    private async Task<Result[]> RaceAsync(TestHost host, string tenant, SqliteDatabase database, ShardOpenCounter counter)
    {
        var hold = counter.HoldNext();
        var allBuilt = _builds.Arm(database.ConnectionString, Racers);
        var first = InvokeAsync(host, tenant);
        await hold.Entered.Task;
        var others = Enumerable.Range(1, Racers - 1).Select(_ => InvokeAsync(host, tenant)).ToList();
        await allBuilt;
        hold.Release.SetResult();
        return await Task.WhenAll([first, .. others]);
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

    /// <summary>
    /// 按分片建 DbContext 的计数信号。
    /// </summary>
    /// <remarks>
    /// <see cref="Arm"/> 之后，给定分片上的建出次数到数即完成返回的任务。
    /// </remarks>
    private sealed class BuildSignal
    {
        /// <summary>
        /// 保护下面三个字段。
        /// </summary>
        private readonly Lock _gate = new();

        /// <summary>
        /// 等的分片连接串。
        /// </summary>
        private string? _target;

        /// <summary>
        /// 还差几次建出。
        /// </summary>
        private int _remaining;

        /// <summary>
        /// 到数时完成。
        /// </summary>
        private TaskCompletionSource? _done;

        /// <summary>
        /// 开始等给定分片上的若干次建出。
        /// </summary>
        public Task Arm(string connectionString, int count)
        {
            lock (_gate)
            {
                _target = connectionString;
                _remaining = count;
                _done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                return _done.Task;
            }
        }

        /// <summary>
        /// 报一次按给定分片建出 DbContext。
        /// </summary>
        public void Built(string connectionString)
        {
            lock (_gate)
            {
                if (_done is not null && string.Equals(_target, connectionString, StringComparison.Ordinal) && --_remaining == 0)
                {
                    _done.SetResult();
                    _done = null;
                }
            }
        }
    }
}
