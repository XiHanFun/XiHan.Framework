// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 健康项 <c>objectstorage.s3.connection</c>。
/// </summary>
/// <remarks>
/// 选项里的健康桶与探测间隔解析；真实服务可用时宿主启动后条目 Ready——配了健康桶就 HEAD 它，关闭条件写入且不配健康桶时列桶（未设置 <c>XIHAN_TEST_S3</c> 时跳过）。健康桶不存在时：条件写入开启即启动失败（探测桶不存在是配置错误，ADR-0093），关闭时照常启动、条目降级（HEAD 失败）。
/// </remarks>
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
    public async Task Health_ListsBuckets_WhenConditionalWritesAreOffAndNoBucketIsConfigured()
    {
        var configuration = S3TestServer.Configuration().Where(static p => !p.Key.EndsWith(":HealthBucket", StringComparison.Ordinal)).ToList();
        configuration.Add(new(S3ObjectStorageOptions.ConfigurationSection + ":ConditionalWrites", "false"));
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<S3ObjectStorageFeature>(), configuration);

        var status = Assert.Single(host.App.Health().Contributions, static c => c.Name == S3ObjectStorageFeature.HealthName);
        Assert.Equal(LifecycleState.Ready, status.State);
    }

    [Fact]
    public async Task MissingHealthBucket_FailsTheStartup_WithConditionalWrites_AndStartsDegradedWithout()
    {
        var missing = "xihan-missing-" + Guid.NewGuid().ToString("N")[..8];
        var configuration = S3TestServer.Configuration().Where(static p => !p.Key.EndsWith(":HealthBucket", StringComparison.Ordinal)).ToList();
        configuration.Add(new(S3ObjectStorageOptions.ConfigurationSection + ":HealthBucket", missing));

        // 条件写入开启：启动探测在桶里写探测键，桶不存在是配置错误——启动失败，原因指出探测桶（ADR-0093）
        var failed = await Assert.ThrowsAsync<FrameworkException>(() => TestHost.StartAsync(static b => b.UseFeature<S3ObjectStorageFeature>(), configuration));
        Assert.Equal(Kernel.KernelErrors.FeatureStartFailed, failed.Code);
        var probe = Assert.IsType<FrameworkException>(failed.InnerException);
        Assert.Equal(ObjectStorageErrors.BlobNotFound, probe.Code);
        Assert.Contains(missing, probe.Message, StringComparison.Ordinal);

        // 关闭条件写入：不探测，HEAD 抛出由探针宿主记成 Degraded；应用仍就绪
        configuration.Add(new(S3ObjectStorageOptions.ConfigurationSection + ":ConditionalWrites", "false"));
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<S3ObjectStorageFeature>(), configuration);
        var disabled = host.App.Health();
        var item = Assert.Single(disabled.Contributions, static c => c.Name == S3ObjectStorageFeature.HealthName);
        Assert.Equal(LifecycleState.Degraded, item.State);
        Assert.Equal(Kernel.KernelErrors.HealthProbeFailed, item.Error?.Code);
        Assert.True(disabled.Ready);
    }
}
