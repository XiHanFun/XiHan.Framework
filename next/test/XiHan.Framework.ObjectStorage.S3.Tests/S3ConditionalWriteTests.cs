// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Multipart;
using XiHan.Framework.ObjectStorage.Abstractions.Stores;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 条件写入探测、启动时探明不认即失败与关闭条件写入（ADR-0089、ADR-0090）。
/// </summary>
/// <remarks>
/// 探测的判定与失败原因用内存替身 <see cref="FakeS3Client"/> 核对，不连服务（尚未确认时的后台重试见 <see cref="S3ConditionalWriteRetryTests"/>）；对真实服务的用例在设置 <c>XIHAN_TEST_S3</c>（认条件写入，如 RustFS）或 <c>XIHAN_TEST_S3_UNCONDITIONAL</c>（不认，如 Zenko CloudServer）时执行。
/// </remarks>
public sealed class S3ConditionalWriteTests
{
    /// <summary>
    /// 替身里的探测桶。
    /// </summary>
    private const string Bucket = "probe";

    /// <summary>
    /// 探测键。
    /// </summary>
    private const string Key = S3ConditionalWriteProbe.ProbeKey;

    [Fact]
    public async Task Probe_PassesOnAServiceThatHonoursConditions_AndKeepsOnlyTheProbeKey()
    {
        var fake = FakeS3Client.Create(Bucket);

        Assert.True((await Probe(fake, multipart: true)).IsSuccess);
        Assert.Equal(["PutObject", "PutObject", "InitiateMultipartUpload", "UploadPart", "CompleteMultipartUpload", "AbortMultipartUpload"], fake.Requests);
        Assert.True(fake.Contains(Bucket, Key));
        Assert.Equal(0, fake.OpenUploads);

        // 再探一次：探测键已在，第一次写入就被拒，即已证明；被拒的分片上传同样放弃
        Assert.True((await Probe(fake, multipart: true)).IsSuccess);
        Assert.Equal(["PutObject", "InitiateMultipartUpload", "UploadPart", "CompleteMultipartUpload", "AbortMultipartUpload"], fake.Requests.Skip(6));
        Assert.Equal(0, fake.OpenUploads);

        // 不提供分片上传时只探单次写入
        Assert.True((await Probe(fake, multipart: false)).IsSuccess);
        Assert.Equal(["PutObject"], fake.Requests.Skip(11));
    }

    [Fact]
    public async Task Probe_ReportsAServiceThatIgnoresConditions_AsUnsupported()
    {
        var ignoring = FakeS3Client.Create(Bucket);
        ignoring.HonoursPutCondition = false;
        var put = await Probe(ignoring, multipart: true);
        Assert.Equal(ObjectStorageErrors.ConditionalWriteUnavailable, put.Error?.Code);
        Assert.Equal(ObjectStorageMessages.ConditionalWriteUnsupported, put.Error?.MessageKey);
        Assert.Equal((Bucket, "PutObject"), ((string?)put.Error!.Details!["bucket"], (string?)put.Error.Details["operation"]));
        Assert.Equal(["PutObject", "PutObject"], ignoring.Requests);

        // 单次写入认条件、分片上传的完成不认：分片上传的不覆盖保证不了，同样失败；完成了的上传不必放弃
        var partial = FakeS3Client.Create(Bucket);
        partial.HonoursCompleteCondition = false;
        var complete = await Probe(partial, multipart: true);
        Assert.Equal(ObjectStorageErrors.ConditionalWriteUnavailable, complete.Error?.Code);
        Assert.Equal("CompleteMultipartUpload", complete.Error!.Details!["operation"]);
        Assert.DoesNotContain("AbortMultipartUpload", partial.Requests);
        Assert.Equal(0, partial.OpenUploads);

        // 服务明说没实现（501）也是不认
        var notImplemented = FakeS3Client.Create(Bucket);
        notImplemented.Fault = static request => request == "PutObject" ? FakeS3Client.ServiceError(HttpStatusCode.NotImplemented, "NotImplemented") : null;
        Assert.Equal(ObjectStorageErrors.ConditionalWriteUnavailable, (await Probe(notImplemented, multipart: true)).Error?.Code);
    }

    [Fact]
    public async Task Probe_DistinguishesDeniedMissingBucketAndUnreachable()
    {
        // 被拒绝：凭据无效或无权写探测键
        var denied = FakeS3Client.Create(Bucket);
        denied.Fault = static request => request == "PutObject" ? FakeS3Client.ServiceError(HttpStatusCode.Forbidden, "AccessDenied") : null;
        var refused = await Probe(denied, multipart: true);
        Assert.Equal(ObjectStorageErrors.ProbeDenied, refused.Error?.Code);
        Assert.Equal(ObjectStorageMessages.ConditionalWriteProbeDenied, refused.Error?.MessageKey);
        Assert.Equal(("PutObject", "AccessDenied", Key), ((string?)refused.Error!.Details!["operation"], (string?)refused.Error.Details["reason"], (string?)refused.Error.Details["key"]));

        // 被拒的分片上传放弃不了（缺放弃分片上传的权限）同样是被拒绝，报出是哪一步
        var abortDenied = FakeS3Client.Create(Bucket);
        abortDenied.Fault = static request => request == "AbortMultipartUpload" ? FakeS3Client.ServiceError(HttpStatusCode.Forbidden, "AccessDenied") : null;
        var abort = await Probe(abortDenied, multipart: true);
        Assert.Equal(ObjectStorageErrors.ProbeDenied, abort.Error?.Code);
        Assert.Equal("AbortMultipartUpload", abort.Error!.Details!["operation"]);

        // 桶不存在
        var missing = await Probe(FakeS3Client.Create(), multipart: true);
        Assert.Equal(ObjectStorageErrors.BlobNotFound, missing.Error?.Code);
        Assert.Equal(ObjectStorageMessages.ConditionalWriteProbeBucketMissing, missing.Error?.MessageKey);

        // 连不上：网络错误，原因带上是哪一步
        var unreachable = FakeS3Client.Create(Bucket);
        unreachable.Fault = static request => request == "PutObject" ? new HttpRequestException("Connection refused.") : null;
        var down = await Probe(unreachable, multipart: true);
        Assert.Equal(ObjectStorageErrors.StoreUnavailable, down.Error?.Code);
        Assert.Contains("Connection refused.", (string?)down.Error!.Details!["reason"], StringComparison.Ordinal);

        // 分片上传中途出错：尽力放弃上传，报原来的错误
        var broken = FakeS3Client.Create(Bucket);
        broken.Fault = static request => request == "UploadPart" ? new HttpRequestException("Connection reset.") : null;
        Assert.Equal(ObjectStorageErrors.StoreUnavailable, (await Probe(broken, multipart: true)).Error?.Code);
        Assert.Equal("AbortMultipartUpload", broken.Requests[^1]);
        Assert.Equal(0, broken.OpenUploads);
    }

    [Fact]
    public async Task Probe_CreatesTheMissingBucket_WhenEnsureBucketsIsOn_AndReportsADeniedCreation()
    {
        var fake = FakeS3Client.Create();

        Assert.True((await Probe(fake, multipart: false, ensureBuckets: true)).IsSuccess);
        Assert.Equal(["PutObject", "PutBucket", "PutObject", "PutObject"], fake.Requests);

        // 建桶被拒：报出是建桶这一步
        var denied = FakeS3Client.Create();
        denied.Fault = static request => request == "PutBucket" ? FakeS3Client.ServiceError(HttpStatusCode.Forbidden, "AccessDenied") : null;
        var refused = await Probe(denied, multipart: false, ensureBuckets: true);
        Assert.Equal(ObjectStorageErrors.ProbeDenied, refused.Error?.Code);
        Assert.Equal("CreateBucket", refused.Error!.Details!["operation"]);
    }

    [Fact]
    public async Task Probe_TimesOut_AsUnavailable_OnTheInjectedClock()
    {
        var fake = FakeS3Client.Create(Bucket);
        fake.Hang = "PutObject";
        var clock = new FakeTimeProvider();
        var options = Options(TimeSpan.FromSeconds(5));

        var probing = S3ConditionalWriteProbe.RunAsync(fake.AsClient(), options, multipart: true, clock, CancellationToken.None).AsTask();
        Assert.False(probing.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(5));
        var result = await probing;

        Assert.Equal(ObjectStorageErrors.StoreUnavailable, result.Error?.Code);
        Assert.Contains("no response within 5 s", (string?)result.Error!.Details!["reason"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Probe_CallerCancellation_Propagates()
    {
        var fake = FakeS3Client.Create(Bucket);
        fake.Hang = "PutObject";
        using var cancellation = new CancellationTokenSource();

        var probing = S3ConditionalWriteProbe.RunAsync(fake.AsClient(), Options(), multipart: true, new FakeTimeProvider(), cancellation.Token).AsTask();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probing);
    }

    [Fact]
    public async Task Probe_ConcurrentInstances_AllConcludeTheSame()
    {
        // 多个实例同时启动、共用一个探测键：先到的写入成功，其余第一次就被拒，结论都是"认条件写入"
        var honouring = FakeS3Client.Create(Bucket);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Probe(honouring, multipart: true)));
        Assert.All(results, static r => Assert.True(r.IsSuccess));
        Assert.Equal(0, honouring.OpenUploads);

        var ignoring = FakeS3Client.Create(Bucket);
        ignoring.HonoursPutCondition = false;
        results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Probe(ignoring, multipart: true)));
        Assert.All(results, static r => Assert.Equal(ObjectStorageErrors.ConditionalWriteUnavailable, r.Error?.Code));
    }

    [Fact]
    public async Task Startup_Fails_WhenTheServiceIgnoresConditionalWrites()
    {
        var fake = FakeS3Client.Create(Bucket);
        fake.HonoursPutCondition = false;

        var failed = await Assert.ThrowsAsync<FrameworkException>(() => StartAsync(fake));
        Assert.Equal(KernelErrors.FeatureStartFailed, failed.Code);
        Assert.Equal(ObjectStorageErrors.ConditionalWriteUnavailable, Assert.IsType<FrameworkException>(failed.InnerException).Code);
        Assert.DoesNotContain("HeadBucket", fake.Requests);
    }

    [Fact]
    public async Task Startup_ProbesBeforeTheHealthCheck()
    {
        var fake = FakeS3Client.Create(Bucket);

        await using var host = await StartAsync(fake);
        Assert.Equal(["PutObject", "PutObject", "InitiateMultipartUpload", "UploadPart", "CompleteMultipartUpload", "AbortMultipartUpload", "HeadBucket"], fake.Requests);
        var status = Assert.Single(host.App.Health().Contributions, static c => c.Name == S3ObjectStorageFeature.HealthName);
        Assert.Equal(LifecycleState.Ready, status.State);
        Assert.Equal("confirmed", status.Data["conditionalWrites"]);
    }

    [Fact]
    public void Assembly_RequiresAProbeBucket_UnlessConditionalWritesAreOff()
    {
        var missing = Assert.Throws<ArgumentException>(static () => Build([new("XiHan:ObjectStorage:S3:ServiceUrl", "http://localhost:9000")]));
        Assert.Contains("'XiHan:ObjectStorage:S3:HealthBucket'", missing.Message, StringComparison.Ordinal);
        Assert.Contains("'XiHan:ObjectStorage:S3:ConditionalWrites'", missing.Message, StringComparison.Ordinal);

        using var provider = Build([new("XiHan:ObjectStorage:S3:ServiceUrl", "http://localhost:9000"), new("XiHan:ObjectStorage:S3:ConditionalWrites", "false")]);
        Assert.False(provider.GetRequiredService<S3ObjectStorageOptions>().ConditionalWrites);
        Assert.Throws<ArgumentException>(static () => S3ObjectStorageOptions.FromSettings(new Dictionary<string, string> { ["ConditionalWrites"] = "maybe" }));
    }

    [Fact]
    public async Task ConditionalWritesOff_SkipsTheProbe_AndRefusesEveryWriteWithoutOverwrite()
    {
        var fake = FakeS3Client.Create(Bucket);

        await using var host = await StartAsync(fake, ("XiHan:ObjectStorage:S3:ConditionalWrites", "false"));
        Assert.Equal(["HeadBucket"], fake.Requests);
        var status = Assert.Single(host.App.Health().Contributions, static c => c.Name == S3ObjectStorageFeature.HealthName);
        Assert.Equal(LifecycleState.Ready, status.State);
        Assert.Equal("disabled", status.Data["conditionalWrites"]);

        // 不覆盖的写入与分片上传一律拒绝，不访问存储；覆盖写入照常
        var store = host.Services.GetRequiredService<IBlobStore>();
        var refused = await store.PutAsync("media", "a.txt", Encoding.UTF8.GetBytes("a"), new BlobPutOptions { Overwrite = false }, CancellationToken.None);
        Assert.Equal(ObjectStorageErrors.ConditionalWriteUnavailable, refused.Error?.Code);
        Assert.Equal(ObjectStorageMessages.ConditionalWriteDisabled, refused.Error?.MessageKey);
        var multipart = host.Services.GetRequiredService<IBlobMultipartStore>();
        Assert.Equal(ObjectStorageErrors.ConditionalWriteUnavailable, (await multipart.StartAsync("media", "b.bin", new BlobPutOptions { Overwrite = false }, CancellationToken.None)).Error?.Code);
        Assert.Equal(ObjectStorageErrors.ConditionalWriteUnavailable, (await multipart.CompleteAsync(new BlobMultipartUpload("media", "b.bin", "upload", overwrite: false), [new BlobPart(1, "etag", 1)], CancellationToken.None)).Error?.Code);
        Assert.Equal(["HeadBucket"], fake.Requests);

        Assert.True((await store.PutAsync(Bucket, "a.txt", Encoding.UTF8.GetBytes("a"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);
        Assert.Equal(["HeadBucket", "PutObject"], fake.Requests);
    }

    [Fact]
    public async Task NamedStore_ProbesOnCreation_CreatesAnUnconfirmedStore_AndRejectsOnlyInvalidDefinitions()
    {
        var registry = new BlobStores([new S3BlobStoreProvider(TimeProvider.System, NullLoggerFactory.Instance)], TimeProvider.System);
        await using (registry)
        {
            // 连不上：建实例时的探测失败只是尚未确认，照常建出并登记；不覆盖写入不可用，原因是连不上
            var down = (await registry.GetOrAddAsync(new BlobStoreDefinition("down", "s3", new Dictionary<string, string> { ["ServiceUrl"] = "http://127.0.0.1:1", ["AccessKey"] = "ak", ["SecretKey"] = "sk", ["HealthBucket"] = Bucket }), CancellationToken.None)).ValueOrThrow();
            Assert.Equal(["down"], registry.Names);
            var refused = await down.PutAsync("media", "a.txt", Encoding.UTF8.GetBytes("a"), new BlobPutOptions { Overwrite = false }, CancellationToken.None);
            Assert.Equal(ObjectStorageErrors.ConditionalWriteUnconfirmed, refused.Error?.Code);
            Assert.Equal(ObjectStorageErrors.StoreUnavailable, (string?)refused.Error!.Details!["reason"]);

            // 没配健康桶：定义不合法
            var invalid = await registry.GetOrAddAsync(new BlobStoreDefinition("nobucket", "s3", new Dictionary<string, string> { ["ServiceUrl"] = "http://127.0.0.1:1" }), CancellationToken.None);
            Assert.Equal(ObjectStorageErrors.StoreDefinitionInvalid, invalid.Error?.Code);

            // 关闭条件写入：建实例不发请求
            var unprobed = (await registry.GetOrAddAsync(new BlobStoreDefinition("unprobed", "s3", new Dictionary<string, string> { ["ServiceUrl"] = "http://127.0.0.1:1", ["ConditionalWrites"] = "false" }), CancellationToken.None)).ValueOrThrow();
            Assert.Equal(ObjectStorageErrors.ConditionalWriteUnavailable, (await unprobed.PutAsync("media", "a.txt", Encoding.UTF8.GetBytes("a"), new BlobPutOptions { Overwrite = false }, CancellationToken.None)).Error?.Code);
        }
    }

    [Fact]
    public async Task RealService_HonoursConditionalWrites_ForDefaultAndNamedStores_AndConcurrentStartups()
    {
        var configuration = S3TestServer.Configuration().ToList();
        var client = S3TestServer.Client();

        // 同一个探测桶上几个实例同时启动：都通过（探测键从不删除，结论与先后无关）
        var fresh = "xihan-probe-" + Guid.NewGuid().ToString("N")[..8];
        S3TestServer.EnsureBucket(client, fresh);
        var concurrent = configuration.Where(static p => !p.Key.EndsWith(":HealthBucket", StringComparison.Ordinal)).Append(new(S3ObjectStorageOptions.ConfigurationSection + ":HealthBucket", fresh)).ToList();
        var hosts = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => TestHost.StartAsync(static b => b.UseFeature<S3ObjectStorageFeature>(), concurrent)));
        foreach (var host in hosts)
        {
            await host.DisposeAsync();
        }

        // 桶里只多一个空的探测键（被拒的分片上传随即放弃，替身用例核对；有的兼容服务列未完成上传时不按桶过滤，这里不列）
        var probe = await client.GetObjectMetadataAsync(fresh, Key);
        Assert.Equal(0L, probe.ContentLength);
        await client.DeleteObjectAsync(fresh, Key);
        await client.DeleteBucketAsync(fresh);

        // 命名实例建出时各探一次
        var registry = new BlobStores([new S3BlobStoreProvider(TimeProvider.System, NullLoggerFactory.Instance)], TimeProvider.System);
        await using (registry)
        {
            var settings = new Dictionary<string, string> { ["ServiceUrl"] = S3TestServer.RequireServiceUrl(), ["AccessKey"] = S3TestServer.AccessKey, ["SecretKey"] = S3TestServer.SecretKey, ["HealthBucket"] = S3TestServer.ProbeBucket };
            Assert.True((await registry.GetOrAddAsync(new BlobStoreDefinition("named", "s3", settings), CancellationToken.None)).IsSuccess);
        }
    }

    [Fact]
    public async Task RealService_RejectedCredentials_StartDegraded_AndRefuseWritesWithoutOverwrite()
    {
        var configuration = S3TestServer.Configuration().Where(static p => !p.Key.EndsWith(":SecretKey", StringComparison.Ordinal)).Append(new(S3ObjectStorageOptions.ConfigurationSection + ":SecretKey", "wrong-secret-key")).ToList();

        // 被拒绝只是尚未确认：照常启动，健康项降级并带出被拒的原因，不覆盖写入不可用
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<S3ObjectStorageFeature>(), configuration);
        var status = Assert.Single(host.App.Health().Contributions, static c => c.Name == S3ObjectStorageFeature.HealthName);
        Assert.Equal((LifecycleState.Degraded, ObjectStorageErrors.ProbeDenied), (status.State, status.Error?.Code));
        Assert.Equal("unconfirmed", status.Data["conditionalWrites"]);
        var refused = await host.Services.GetRequiredService<IBlobStore>().PutAsync(S3TestServer.ProbeBucket, Guid.NewGuid().ToString("N"), Encoding.UTF8.GetBytes("a"), new BlobPutOptions { Overwrite = false }, CancellationToken.None);
        Assert.Equal(ObjectStorageErrors.ConditionalWriteUnconfirmed, refused.Error?.Code);
        Assert.Equal(ObjectStorageErrors.ProbeDenied, (string?)refused.Error!.Details!["reason"]);
    }

    [Fact]
    public async Task UnconditionalService_FailsTheStartup_AndTheNamedStoreCreation()
    {
        var url = S3TestServer.RequireUnconditionalServiceUrl();
        using (var client = S3ObjectStorageFeature.CreateClient(new S3ObjectStorageOptions { ServiceUrl = url, AccessKey = S3TestServer.AccessKey, SecretKey = S3TestServer.SecretKey }))
        {
            S3TestServer.EnsureBucket(client, S3TestServer.ProbeBucket);
        }

        var failed = await Assert.ThrowsAsync<FrameworkException>(() => TestHost.StartAsync(
            static b => b.UseFeature<S3ObjectStorageFeature>(),
            [
                new(S3ObjectStorageOptions.ConfigurationSection + ":ServiceUrl", url),
                new(S3ObjectStorageOptions.ConfigurationSection + ":AccessKey", S3TestServer.AccessKey),
                new(S3ObjectStorageOptions.ConfigurationSection + ":SecretKey", S3TestServer.SecretKey),
                new(S3ObjectStorageOptions.ConfigurationSection + ":HealthBucket", S3TestServer.ProbeBucket),
            ]));
        var probe = Assert.IsType<FrameworkException>(failed.InnerException);
        Assert.Equal(ObjectStorageErrors.ConditionalWriteUnavailable, probe.Code);

        var registry = new BlobStores([new S3BlobStoreProvider(TimeProvider.System, NullLoggerFactory.Instance)], TimeProvider.System);
        await using (registry)
        {
            var settings = new Dictionary<string, string> { ["ServiceUrl"] = url, ["AccessKey"] = S3TestServer.AccessKey, ["SecretKey"] = S3TestServer.SecretKey, ["HealthBucket"] = S3TestServer.ProbeBucket };
            Assert.Equal(ObjectStorageErrors.ConditionalWriteUnavailable, (await registry.GetOrAddAsync(new BlobStoreDefinition("unconditional", "s3", settings), CancellationToken.None)).Error?.Code);
            Assert.Empty(registry.Names);
        }
    }

    /// <summary>
    /// 对替身探测一次。
    /// </summary>
    private static async Task<Result> Probe(FakeS3Client fake, bool multipart, bool ensureBuckets = false)
        => await S3ConditionalWriteProbe.RunAsync(fake.AsClient(), Options() with { EnsureBuckets = ensureBuckets }, multipart, TimeProvider.System, CancellationToken.None);

    /// <summary>
    /// 以替身的探测桶为健康桶的选项。
    /// </summary>
    private static S3ObjectStorageOptions Options(TimeSpan? interval = null)
        => new() { ServiceUrl = "http://localhost:9000", HealthBucket = Bucket, HealthProbeInterval = interval ?? TimeSpan.FromSeconds(15) };

    /// <summary>
    /// 以替身为客户端启动只装 S3 的宿主。
    /// </summary>
    private static Task<TestHost> StartAsync(FakeS3Client fake, params (string Key, string Value)[] extra)
        => TestHost.StartAsync(
            static b => b.UseFeature<S3ObjectStorageFeature>(),
            [
                new("XiHan:ObjectStorage:S3:ServiceUrl", "http://localhost:9000"),
                new("XiHan:ObjectStorage:S3:HealthBucket", Bucket),
                .. extra.Select(static e => new KeyValuePair<string, string?>(e.Key, e.Value)),
            ],
            services => services.AddSingleton(fake.AsClient()));

    /// <summary>
    /// 只装配不启动，返回容器。
    /// </summary>
    private static ServiceProvider Build(IEnumerable<KeyValuePair<string, string?>> configuration)
    {
        var services = new ServiceCollection();
        var builder = new AppBuilder(services, new ConfigurationBuilder().AddInMemoryCollection(configuration).Build());
        builder.UseFeature<S3ObjectStorageFeature>();
        builder.Build();
        return services.BuildServiceProvider();
    }
}
