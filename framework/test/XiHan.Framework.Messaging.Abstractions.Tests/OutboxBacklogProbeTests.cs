// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Abstractions.Outbox;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Messaging.Abstractions.Tests;

/// <summary>
/// 发件箱健康探针读不出时怎么报告（ADR-0272）
/// </summary>
/// <remarks>
/// 以按脚本应答的工作单元与仓储验证：探针开只读工作单元；仓储抛出、提交返回失败都让探测失败，条目 Degraded（<c>KERNEL_UNAVAILABLE_005</c>，原因只写异常类型），上一次数出的数字作废。阈值与计数在内存持久化上的用例见 <c>Messaging.InMemory.Tests</c> 的 <c>OutboxHealthTests</c>
/// </remarks>
public sealed class OutboxBacklogProbeTests
{
    /// <summary>
    /// 健康项名
    /// </summary>
    private const string HealthName = OutboxFeature.HealthName;

    [Fact]
    public async Task Probe_ReadsThroughAReadOnlyUnitOfWork_AndKeepsTheNumbersItCounted()
    {
        var unitOfWork = new ScriptedUnitOfWork();
        var row = new OutboxMessage { Id = Guid.CreateVersion7(), Topic = "orders", CreatedAt = TimeProvider.System.GetUtcNow().AddMinutes(-1) };
        await using var services = Services(() => unitOfWork, () => new ScriptedRepository<OutboxMessage, Guid> { Found = row });
        var probe = Probe(services, new OutboxOptions { HealthMaxDeadLettered = 5 });

        var result = await probe.ProbeAsync(CancellationToken.None);

        // 按脚本应答的仓储不看查询：未投递与死信各数到这一行，都在阈值之内
        Assert.True(result.IsSuccess, result.ToString());
        Assert.Equal(["begin", "commit", "dispose"], unitOfWork.Calls);
        Assert.True(unitOfWork.Options.IsReadOnly);
        Assert.Null(unitOfWork.Options.TenantId);
        var backlog = Assert.IsType<OutboxBacklog>(probe.Backlog);
        Assert.Equal((1L, 1L, 1), (backlog.Pending, backlog.DeadLettered, backlog.Databases));
        Assert.InRange(backlog.OldestPendingAge!.Value, TimeSpan.FromSeconds(59), TimeSpan.FromMinutes(2));
        Assert.Equal(["pending", "oldestPendingSeconds", "deadLettered", "databases"], probe.Data().Keys);
    }

    [Fact]
    public async Task ProbeThatCannotReadTheOutbox_Throws_AndTheItemIsDegradedWithoutStaleNumbers()
    {
        var failing = false;
        var failure = new TimeoutException("Server=db-01;Password=secret");
        var row = new OutboxMessage { Id = Guid.CreateVersion7(), Topic = "orders", CreatedAt = TimeProvider.System.GetUtcNow() };
        await using var services = Services(static () => new ScriptedUnitOfWork(), () => failing ? new ScriptedRepository<OutboxMessage, Guid> { Failure = failure } : new ScriptedRepository<OutboxMessage, Guid> { Found = row });
        using var logs = new LogCapture();
        var probe = Probe(services, new OutboxOptions { HealthMaxDeadLettered = 5 });
        await using var periodic = new PeriodicHealthContributor(HealthName, probe.ProbeAsync, TimeSpan.FromSeconds(30), TimeProvider.System, logs);
        var health = new OutboxHealth(periodic, probe);

        // 先成功探一次：条目 Ready，带着数出的数字
        await periodic.ProbeAsync(CancellationToken.None);
        var ready = Assert.Single(health.Check());
        Assert.Equal((HealthName, LifecycleState.Ready), (ready.Name, ready.State));
        Assert.Equal(1L, ready.Data["pending"]);

        // 数据库断开：探针把仓储的异常原样抛出
        failing = true;
        Assert.Same(failure, await Assert.ThrowsAsync<TimeoutException>(async () => await probe.ProbeAsync(CancellationToken.None)));
        Assert.Null(probe.Backlog);

        // 探针宿主把它记成 Degraded：原因只写异常类型，异常消息不进错误；上一次的数字不再展示
        await periodic.ProbeAsync(CancellationToken.None);
        var degraded = Assert.Single(health.Check());
        Assert.Equal(LifecycleState.Degraded, degraded.State);
        Assert.Equal(KernelErrors.HealthProbeFailed, degraded.Error?.Code);
        Assert.Equal(typeof(TimeoutException).FullName, degraded.Error?.Details?["reason"]);
        Assert.DoesNotContain("secret", degraded.Error!.Message, StringComparison.Ordinal);
        Assert.Empty(degraded.Data);
        // 完整异常进日志，记在探针宿主的类别下
        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Warning && ReferenceEquals(e.Exception, failure));

        // 恢复之后数字回来
        failing = false;
        await periodic.ProbeAsync(CancellationToken.None);
        var recovered = Assert.Single(health.Check());
        Assert.Equal(LifecycleState.Ready, recovered.State);
        Assert.Equal(1L, recovered.Data["pending"]);
    }

    [Fact]
    public async Task ProbeWhoseCommitFails_Throws_WithTheErrorCodeOfTheCommit()
    {
        var unitOfWork = new ScriptedUnitOfWork { CommitResult = Error.Literal("PERSISTENCE_UNAVAILABLE_003", "The store is unavailable.") };
        await using var services = Services(() => unitOfWork, static () => new ScriptedRepository<OutboxMessage, Guid>());
        var probe = Probe(services, new OutboxOptions());

        // 只读工作单元的提交返回失败同样是读不出：不把数到一半的数字当成结果
        var thrown = await Assert.ThrowsAsync<FrameworkException>(async () => await probe.ProbeAsync(CancellationToken.None));

        Assert.Equal("PERSISTENCE_UNAVAILABLE_003", thrown.Code);
        Assert.Null(probe.Backlog);
        Assert.Empty(probe.Data());
    }

    [Fact]
    public async Task HealthItem_BeforeTheFirstProbe_HasNoNumbers()
    {
        await using var services = Services(static () => new ScriptedUnitOfWork(), static () => new ScriptedRepository<OutboxMessage, Guid>());
        var probe = Probe(services, new OutboxOptions());
        await using var periodic = new PeriodicHealthContributor(HealthName, probe.ProbeAsync, TimeSpan.FromSeconds(30), TimeProvider.System, NullLoggerFactory.Instance);

        var status = Assert.Single(new OutboxHealth(periodic, probe).Check());

        Assert.Equal((HealthName, LifecycleState.Started), (status.Name, status.State));
        Assert.Empty(status.Data);
    }

    /// <summary>
    /// 只有按脚本应答的工作单元与发件箱仓储的容器
    /// </summary>
    /// <remarks>
    /// 工作单元与仓储每个作用域各取一次：每次探测一个新的工作单元，用例可以在两次探测之间换掉仓储的应答
    /// </remarks>
    private static ServiceProvider Services(Func<ScriptedUnitOfWork> unitOfWork, Func<ScriptedRepository<OutboxMessage, Guid>> repository)
        => new ServiceCollection()
            .AddScoped<IUnitOfWork>(_ => unitOfWork())
            .AddScoped<IRepository<OutboxMessage, Guid>>(_ => repository())
            .BuildServiceProvider();

    /// <summary>
    /// 在给定容器上建探针：没有装配租户分片，只有默认库
    /// </summary>
    private static OutboxBacklogProbe Probe(IServiceProvider services, OutboxOptions options)
        => new(new OutboxDatabases(services, options, TimeProvider.System), options, TimeProvider.System, NullLogger.Instance);
}
