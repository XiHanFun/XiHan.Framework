// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Caching.InMemory;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions.Identifiers;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.Abstractions.Tests;

/// <summary>
/// <see cref="SnowflakeIdFeature"/> 的装配。
/// </summary>
/// <remarks>
/// 配置了机器位时固定来源、没有配置时经分布式锁租约分配并上报健康项、既没配置也没有锁时启动失败。
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

    /// <summary>
    /// 异常链上的全部框架错误码。
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
