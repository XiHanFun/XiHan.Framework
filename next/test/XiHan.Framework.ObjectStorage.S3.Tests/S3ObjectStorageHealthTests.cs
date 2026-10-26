// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 健康项 <c>objectstorage.s3.connection</c>：选项里的健康桶与探测间隔解析；真实服务可用时宿主启动后条目 Ready——不配健康桶时列桶，配了就 HEAD 它（未设置 <c>XIHAN_TEST_S3</c> 时跳过）。
/// </summary>
public sealed class S3ObjectStorageHealthTests
{
    [Fact]
    public void Options_ParseHealthBucketAndInterval_AndRejectNonPositiveInterval()
    {
        static IConfiguration Build(params (string Key, string Value)[] values)
            => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

        var defaults = S3ObjectStorageOptions.FromConfiguration(Build());
        Assert.Null(defaults.HealthBucket);
        Assert.Equal(TimeSpan.FromSeconds(15), defaults.HealthProbeInterval);

        var options = S3ObjectStorageOptions.FromConfiguration(Build(("XiHan:ObjectStorage:S3:HealthBucket", "xihan-health"), ("XiHan:ObjectStorage:S3:HealthProbeIntervalSeconds", "30")));
        Assert.Equal("xihan-health", options.HealthBucket);
        Assert.Equal(TimeSpan.FromSeconds(30), options.HealthProbeInterval);
        Assert.Throws<ArgumentException>(() => S3ObjectStorageOptions.FromConfiguration(Build(("XiHan:ObjectStorage:S3:HealthProbeIntervalSeconds", "0"))));
    }

    [Fact]
    public async Task Health_ReportsConnectionReady_AgainstARealService()
    {
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<S3ObjectStorageFeature>(), S3TestServer.Configuration());

        var health = host.App.Health();
        var status = Assert.Single(health.Contributions, static c => c.Name == S3ObjectStorageFeature.HealthName);
        Assert.Equal(LifecycleState.Ready, status.State);
        Assert.Null(status.Error);
        Assert.True(health.Ready);
    }

    [Fact]
    public async Task Health_HeadsTheConfiguredBucket_AndDegradesWhenItIsMissing()
    {
        var configuration = S3TestServer.Configuration().ToList();
        configuration.Add(new(S3ObjectStorageOptions.ConfigurationSection + ":HealthBucket", "xihan-missing-" + Guid.NewGuid().ToString("N")[..8]));
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<S3ObjectStorageFeature>(), configuration);

        // 桶不存在：HEAD 抛出，由探针宿主记成 Degraded；应用仍就绪
        var health = host.App.Health();
        var status = Assert.Single(health.Contributions, static c => c.Name == S3ObjectStorageFeature.HealthName);
        Assert.Equal(LifecycleState.Degraded, status.State);
        Assert.Equal(Kernel.KernelErrors.HealthProbeFailed, status.Error?.Code);
        Assert.True(health.Ready);
    }
}
