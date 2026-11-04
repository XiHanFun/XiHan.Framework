// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Caching.Abstractions.Locking;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Caching.InMemory;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Persistence.Abstractions.Identifiers;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.Abstractions.Tests;

/// <summary>
/// <see cref="SnowflakeIdFeature"/> 的装配
/// </summary>
/// <remarks>
/// 配置了机器位时固定来源、没有配置时经分布式锁租约分配并上报健康项、既没配置也没有锁时启动失败；缓存 Provider 写在后面也先于本特性启动、后于本特性停止；调用里没有机器位或时钟回拨按 UNAVAILABLE 结果结束（HTTP 503），别的异常仍是 <c>KERNEL_UNEXPECTED_001</c>
/// </remarks>
public sealed class SnowflakeIdFeatureTests
{
    [Fact]
    public async Task ConfiguredWorkerId_IsFixed_AndNoHealthItemIsRegistered()
    {
        await using var host = await TestHost.StartAsync(
            static x => x.UseFeature<SnowflakeIdFeature>(),
            [new KeyValuePair<string, string?>($"{SnowflakeOptions.ConfigurationSection}:WorkerId", "42")]);

        var generator = host.Services.GetRequiredService<IIdGenerator<long>>();
        var parsed = SnowflakeId.Parse(generator.NewId(), SnowflakeOptions.DefaultEpoch);

        Assert.Equal(42, parsed.WorkerId);
        Assert.IsType<FixedWorkerIdSource>(host.Services.GetRequiredService<IWorkerIdSource>());
        // 可选依赖 caching.distributed 缺失：按 RFC-0002 特性是 Degraded，生成照常
        Assert.Equal(LifecycleState.Degraded, Feature(host).State);
        Assert.DoesNotContain(host.Services.GetServices<IHealthContributor>(), static c => c is LeasedWorkerIdSource);
        Assert.Same(generator, host.Services.GetRequiredService<SnowflakeIdGenerator>());
    }

    [Fact]
    public async Task WithoutConfiguredWorkerId_LeasesOneFromTheDistributedLock_AndReportsHealth()
    {
        await using var host = await TestHost.StartAsync(static x => x.UseFeature<InMemoryCachingFeature>().UseFeature<SnowflakeIdFeature>());

        var generator = host.Services.GetRequiredService<SnowflakeIdGenerator>();
        var parsed = generator.Parse(generator.NewId());
        var source = Assert.IsType<LeasedWorkerIdSource>(host.Services.GetRequiredService<IWorkerIdSource>());
        var status = Assert.Single(Assert.Single(host.Services.GetServices<IHealthContributor>().OfType<LeasedWorkerIdSource>()).Check());

        Assert.Equal(0, parsed.WorkerId);
        Assert.Equal(0, source.Current.ValueOrThrow());
        Assert.Equal(LeasedWorkerIdSource.HealthName, status.Name);
        Assert.Equal(LifecycleState.Ready, status.State);

        // 停机释放租约
        await host.App.StopAsync(CancellationToken.None);
        Assert.False(source.Current.HasValue);
    }

    [Fact]
    public async Task WithoutConfiguredWorkerId_AndWithoutDistributedLock_FailsStartup()
    {
        var exception = await Assert.ThrowsAsync<FrameworkException>(() => TestHost.StartAsync(static x => x.UseFeature<SnowflakeIdFeature>()));

        Assert.Contains(Codes(exception), static code => code == PersistenceErrors.WorkerIdUnavailable);
        Assert.Contains("caching.distributed", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CachingProviderAssembledAfterTheFeature_StartsBefore_AndStopsAfterIt()
    {
        // 缓存 Provider 写在后面：能力图谱仍把它排在前面，租约在锁存储可用时取得、停止时先释放再停存储
        var caching = new StartStopCachingFeature();
        await using var host = await TestHost.StartAsync(x => x.UseFeature<SnowflakeIdFeature>().UseFeature(caching));
        var source = Assert.IsType<LeasedWorkerIdSource>(host.Services.GetRequiredService<IWorkerIdSource>());
        Assert.Equal(0, source.Current.ValueOrThrow());
        Assert.Equal(LifecycleState.Ready, Feature(host).State);
        Assert.True((await caching.Inner.GetAsync(LockKey(0), CancellationToken.None)).HasValue);

        await host.App.StopAsync(CancellationToken.None);

        Assert.False(source.Current.HasValue);
        Assert.Equal(LifecycleState.Stopped, Feature(host).State);
        Assert.False((await caching.Inner.GetAsync(LockKey(0), CancellationToken.None)).HasValue);
    }

    [Fact]
    public async Task NewId_WithoutWorkerId_InAnInvocation_FailsAsUnavailable()
    {
        await using var host = await TestHost.StartAsync(static x => x.UseFeature<InMemoryCachingFeature>().UseFeature<SnowflakeIdFeature>());
        // 租约释放后没有机器位
        await host.Services.GetRequiredService<LeasedWorkerIdSource>().StopAsync(CancellationToken.None);
        var generator = host.Services.GetRequiredService<IIdGenerator<long>>();

        var outcome = await host.InvokeAsync(context =>
        {
            context.SetOutcome(Result.Success());
            _ = generator.NewId();
            return ValueTask.CompletedTask;
        });

        // UNAVAILABLE 种类：协议适配器映射为 503 / gRPC Unavailable，而不是 KERNEL_UNEXPECTED_001 的 500
        Assert.Equal(PersistenceErrors.WorkerIdUnavailable, outcome.Error?.Code);
        Assert.Equal("UNAVAILABLE", outcome.Error!.Kind);
        Assert.Equal(PersistenceMessages.WorkerIdUnavailable, outcome.Error.MessageKey);
    }

    [Fact]
    public async Task NewId_AfterTheClockMovedBackBeyondTolerance_InAnInvocation_FailsAsUnavailable()
    {
        var clock = new ManualClock(new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero));
        await using var host = await TestHost.StartAsync(
            static x => x.UseFeature<SnowflakeIdFeature>(),
            [new KeyValuePair<string, string?>($"{SnowflakeOptions.ConfigurationSection}:WorkerId", "1")],
            services => services.AddSingleton<TimeProvider>(clock));
        var generator = host.Services.GetRequiredService<IIdGenerator<long>>();
        _ = generator.NewId();
        clock.Set(clock.GetUtcNow().AddSeconds(-1));

        var outcome = await host.InvokeAsync(context =>
        {
            context.SetOutcome(Result.Success());
            _ = generator.NewId();
            return ValueTask.CompletedTask;
        });

        Assert.Equal(PersistenceErrors.ClockMovedBackwards, outcome.Error?.Code);
        Assert.Equal("UNAVAILABLE", outcome.Error!.Kind);
        Assert.Equal(PersistenceMessages.ClockMovedBackwards, outcome.Error.MessageKey);
    }

    [Fact]
    public async Task OtherExceptions_InAnInvocation_StillBecomeUnexpected()
    {
        await using var host = await TestHost.StartAsync(
            static x => x.UseFeature<SnowflakeIdFeature>(),
            [new KeyValuePair<string, string?>($"{SnowflakeOptions.ConfigurationSection}:WorkerId", "1")]);

        // 只转本特性的两个错误码：别的框架异常照旧由 Observe 阶段包成 KERNEL_UNEXPECTED_001
        var outcome = await host.InvokeAsync(static _ => throw new FrameworkException(PersistenceErrors.NoActiveUnitOfWork, "The repository was used outside the Transaction stage."));

        Assert.Equal(KernelErrors.Unexpected, outcome.Error?.Code);
        Assert.Equal(PersistenceErrors.NoActiveUnitOfWork, outcome.Error!.Inner?.Code);
        Assert.Contains(host.App.DescribePipeline().Split('\n'), static line => line.StartsWith("resilience", StringComparison.Ordinal) && line.Contains(typeof(SnowflakeUnavailableMiddleware).FullName!, StringComparison.Ordinal));
    }

    /// <summary>
    /// 雪花特性的状态
    /// </summary>
    private static FeatureStatus Feature(TestHost host) => host.App.Health().Features.Single(static f => f.Manifest.Id == "xihan.persistence.snowflake");

    /// <summary>
    /// 机器位在存储里的锁条目键（<see cref="StoreDistributedLock"/> 的前缀 + 资源名）
    /// </summary>
    private static CacheKey LockKey(int workerId) => new("lock:" + LeasedWorkerIdSource.ResourcePrefix + workerId);

    /// <summary>
    /// 异常链上的全部框架错误码
    /// </summary>
    private static IEnumerable<string> Codes(Exception? exception)
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            if (exception is FrameworkException framework)
            {
                yield return framework.Code;
            }
        }
    }
}
