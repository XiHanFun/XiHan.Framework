// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Multipart;
using XiHan.Framework.ObjectStorage.Abstractions.Stores;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 启动时连不上照常启动、后台重试与写入时就地探测
/// </summary>
/// <remarks>
/// 尚未确认时的行为（ADR-0090），以及启动之后才被拒绝或缺桶（ADR-0093）。照常启动的只有启动时连不上（启动时被拒绝、缺桶即启动失败，见 <see cref="S3ConditionalWriteTests"/>）。用内存替身 <see cref="FakeS3Client"/> 控制服务何时连得上、何时认条件写入，用 <see cref="FakeTimeProvider"/> 推进退避，不靠真实等待；推进之前先等后台重试排定计时器（<see cref="S3ConditionalWriteGate.WhenRetryScheduledAsync"/>）。等待都带真实时间的上限，只为失败时不挂死
/// </remarks>
public sealed class S3ConditionalWriteRetryTests
{
    /// <summary>
    /// 替身里的探测桶，也是用例写入的桶
    /// </summary>
    private const string Bucket = "probe";

    [Fact]
    public async Task Startup_WhenTheProbeCannotConnect_StartsDegraded_RefusesWritesWithoutOverwrite_AndRecoversInTheBackground()
    {
        var fake = FakeS3Client.Create(Bucket);
        fake.Fault = static request => request == "PutObject" ? new HttpRequestException("Connection refused.") : null;
        var clock = new FakeTimeProvider();
        await using var host = await StartAsync(fake, clock);

        // 照常启动：健康项降级，原因是探测连不上（连通性探测本身成功也一样），数据标明尚未确认；应用仍就绪
        var status = Connection(host);
        Assert.Equal(LifecycleState.Degraded, status.State);
        Assert.Equal(ObjectStorageErrors.StoreUnavailable, status.Error?.Code);
        Assert.Equal("unconfirmed", status.Data["conditionalWrites"]);
        Assert.True(host.App.Health().Ready);

        // 探明之前不覆盖写入与分片上传的发起返回不可用；距上一次探测不足 1 秒，不再探测、不访问写入目标
        var store = host.Services.GetRequiredService<IBlobStore>();
        var requests = fake.Requests.Count;
        var refused = await Put(store, "a.txt");
        Assert.Equal(ObjectStorageErrors.ConditionalWriteUnconfirmed, refused.Error?.Code);
        Assert.Equal(ObjectStorageMessages.ConditionalWriteUnconfirmed, refused.Error?.MessageKey);
        Assert.Equal(ObjectStorageErrors.StoreUnavailable, (string?)refused.Error!.Details!["reason"]);
        var multipart = host.Services.GetRequiredService<IBlobMultipartStore>();
        Assert.Equal(ObjectStorageErrors.ConditionalWriteUnconfirmed, (await multipart.StartAsync(new ContainerName(Bucket), new BlobKey("b.bin"), new BlobPutOptions { Overwrite = false }, CancellationToken.None)).Error?.Code);
        Assert.Equal(requests, fake.Requests.Count);

        // 后台重试：1 秒后再探仍连不上，下一次排在 2 秒后；服务恢复后那一次探测通过
        var gate = host.Services.GetRequiredService<S3ConditionalWriteGate>();
        await Within(gate.WhenRetryScheduledAsync(1));
        clock.Advance(TimeSpan.FromSeconds(1));
        await Within(gate.WhenRetryScheduledAsync(2));
        Assert.False(gate.Settled.IsCompleted);
        fake.Fault = static _ => null;
        clock.Advance(TimeSpan.FromSeconds(2));
        await Within(gate.Settled);

        // 健康项即时恢复，不等下一轮连通性探测；不覆盖写入照常，原子性照旧由条件写入保证
        status = Connection(host);
        Assert.Equal(LifecycleState.Ready, status.State);
        Assert.Equal("confirmed", status.Data["conditionalWrites"]);
        Assert.True((await Put(store, "a.txt")).IsSuccess);
        Assert.Equal(ObjectStorageErrors.BlobExists, (await Put(store, "a.txt")).Error?.Code);

        // 有了终态即停止重试：只排定过两次
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)], gate.ScheduledDelays);
    }

    [Fact]
    public async Task WritesWithoutOverwrite_ShareOneOnDemandProbe_OnceTheLastAttemptIsASecondOld()
    {
        var fake = FakeS3Client.Create(Bucket);
        fake.Fault = static request => request == "PutObject" ? new HttpRequestException("Connection refused.") : null;
        var clock = new FakeTimeProvider();
        await using var host = await StartAsync(fake, clock);
        var gate = host.Services.GetRequiredService<S3ConditionalWriteGate>();
        var store = host.Services.GetRequiredService<IBlobStore>();

        // 第 1 秒的重试仍失败，下一次排在第 3 秒
        await Within(gate.WhenRetryScheduledAsync(1));
        clock.Advance(TimeSpan.FromSeconds(1));
        await Within(gate.WhenRetryScheduledAsync(2));

        // 第 2 秒服务恢复：后台还没到点，距上一次探测已满 1 秒，写入就地探测；探测被拦住期间到来的写入共用它
        fake.Fault = static _ => null;
        fake.Held = "PutObject";
        clock.Advance(TimeSpan.FromSeconds(1));
        var before = fake.Requests.Count;
        var writes = Enumerable.Range(0, 3).Select(index => Put(store, $"w{index}.txt")).ToArray();
        await Within(fake.HeldArrived);
        Assert.All(writes, static write => Assert.False(write.IsCompleted));
        Assert.Equal(["PutObject"], fake.Requests.Skip(before));

        fake.Release();
        var results = await Within(Task.WhenAll(writes));
        Assert.All(results, static result => Assert.True(result.IsSuccess));
        Assert.True(gate.Settled.IsCompleted);
        Assert.Single(fake.Requests.Skip(before), static request => request == "InitiateMultipartUpload");
    }

    [Fact]
    public async Task Background_FindingTheProbeDenied_FailsTheHealthItem_KeepsRetrying_AndRecoversOncePermissionIsGranted()
    {
        var fake = FakeS3Client.Create(Bucket);
        fake.Fault = static request => request == "PutObject" ? new HttpRequestException("Connection refused.") : null;
        var clock = new FakeTimeProvider();
        var logs = new LogCapture();
        await using var host = await StartAsync(fake, clock, logs);
        var gate = host.Services.GetRequiredService<S3ConditionalWriteGate>();
        var store = host.Services.GetRequiredService<IBlobStore>();

        // 启动时连不上照常启动；第 1 秒连上时凭据已被吊销（或配置本来就错，启动时探不到）
        await Within(gate.WhenRetryScheduledAsync(1));
        fake.Fault = static request => request == "PutObject" ? FakeS3Client.ServiceError(HttpStatusCode.Forbidden, "AccessDenied") : null;
        clock.Advance(TimeSpan.FromSeconds(1));
        await Within(gate.WhenRetryScheduledAsync(2));

        // 配置错误要人处理：健康项 Failed（不混同于连不上的 Degraded），应用不停止、仍就绪；
        // 不覆盖写入仍是暂不可用（503），原因是被拒，不访问写入目标
        var status = Connection(host);
        Assert.Equal((LifecycleState.Failed, ObjectStorageErrors.ProbeDenied, ObjectStorageMessages.ConditionalWriteProbeDenied), (status.State, status.Error?.Code, status.Error?.MessageKey));
        Assert.Equal("unconfirmed", status.Data["conditionalWrites"]);
        Assert.True(host.App.Health().Ready);
        var requests = fake.Requests.Count;
        var refused = await Put(store, "a.txt");
        Assert.Equal((ObjectStorageErrors.ConditionalWriteUnconfirmed, ObjectStorageErrors.ProbeDenied), (refused.Error?.Code, (string?)refused.Error!.Details!["reason"]));
        Assert.Equal(requests, fake.Requests.Count);

        // 照常重试：下一次排在 2 秒后；权限改好后那一次探测通过，不必重启
        Assert.False(gate.Settled.IsCompleted);
        fake.Fault = static _ => null;
        clock.Advance(TimeSpan.FromSeconds(2));
        await Within(gate.Settled);
        status = Connection(host);
        Assert.Equal((LifecycleState.Ready, "confirmed"), (status.State, status.Data["conditionalWrites"]));
        Assert.True((await Put(store, "a.txt")).IsSuccess);

        // 日志：连不上是 Warning，之后被拒是 Error，确认是 Information，各一条
        // 第一条是连不上时带完整异常的探测日志，之后才是状态变化
        Assert.Equal([LogLevel.Warning, LogLevel.Warning, LogLevel.Error, LogLevel.Information], GateLogs(logs).Select(static e => e.Level));
        Assert.Contains(ObjectStorageErrors.ProbeDenied, GateLogs(logs)[2].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Background_FindingTheProbeBucketMissing_FailsTheHealthItem_AndRecoversOnceTheBucketExists()
    {
        var fake = FakeS3Client.Create();
        fake.Fault = static request => request == "PutObject" ? new HttpRequestException("Connection refused.") : null;
        var clock = new FakeTimeProvider();
        await using var host = await StartAsync(fake, clock);
        var gate = host.Services.GetRequiredService<S3ConditionalWriteGate>();

        // 启动时连不上照常启动；连上时探测桶不存在（被删了，或配置本来就错）
        await Within(gate.WhenRetryScheduledAsync(1));
        fake.Fault = static _ => null;
        clock.Advance(TimeSpan.FromSeconds(1));
        await Within(gate.WhenRetryScheduledAsync(2));

        // 健康项 Failed，错误是探测桶不存在（而不是笼统的连通性失败）；不覆盖写入暂不可用，原因是缺桶
        var status = Connection(host);
        Assert.Equal((LifecycleState.Failed, ObjectStorageErrors.BlobNotFound, ObjectStorageMessages.ConditionalWriteProbeBucketMissing), (status.State, status.Error?.Code, status.Error?.MessageKey));
        Assert.Equal(ObjectStorageErrors.BlobNotFound, (string?)(await Put(host.Services.GetRequiredService<IBlobStore>(), "a.txt")).Error!.Details!["reason"]);

        // 运维建桶：后台的下一次探测通过（条目的连通性部分照旧等下一轮 HEAD，启动时那次 HEAD 也因缺桶失败）
        fake.AddBucket(Bucket);
        clock.Advance(TimeSpan.FromSeconds(2));
        await Within(gate.Settled);
        Assert.Equal("confirmed", Connection(host).Data["conditionalWrites"]);
        Assert.True((await Put(host.Services.GetRequiredService<IBlobStore>(), "a.txt")).IsSuccess);
    }

    [Fact]
    public async Task Background_FindingTheServiceIgnoresConditions_FailsTheHealthItem_AndRefusesWritesAsUnsupported()
    {
        var fake = FakeS3Client.Create(Bucket);
        fake.Fault = static request => request == "PutObject" ? new HttpRequestException("Connection refused.") : null;
        var clock = new FakeTimeProvider();
        await using var host = await StartAsync(fake, clock);
        var gate = host.Services.GetRequiredService<S3ConditionalWriteGate>();
        var store = host.Services.GetRequiredService<IBlobStore>();

        // 启动时连不上，之后连上的是一个不认条件写入的服务
        await Within(gate.WhenRetryScheduledAsync(1));
        fake.Fault = static _ => null;
        fake.HonoursPutCondition = false;
        clock.Advance(TimeSpan.FromSeconds(1));
        await Within(gate.Settled);

        // 不认是终态：健康项 Failed（应用仍在服务），不覆盖写入一律明确报错，不再访问存储，也不再重试
        var status = Connection(host);
        Assert.Equal((LifecycleState.Failed, ObjectStorageErrors.ConditionalWriteUnavailable), (status.State, status.Error?.Code));
        Assert.Equal("unsupported", status.Data["conditionalWrites"]);
        Assert.True(host.App.Health().Ready);
        var requests = fake.Requests.Count;
        var refused = await Put(store, "a.txt");
        Assert.Equal((ObjectStorageErrors.ConditionalWriteUnavailable, ObjectStorageMessages.ConditionalWriteUnsupported), (refused.Error?.Code, refused.Error?.MessageKey));
        var multipart = host.Services.GetRequiredService<IBlobMultipartStore>();
        Assert.Equal(ObjectStorageErrors.ConditionalWriteUnavailable, (await multipart.CompleteAsync(new BlobMultipartUpload(new ContainerName(Bucket), new BlobKey("b.bin"), "upload") { Overwrite = false }, [new BlobPart(1, "etag", 1)], CancellationToken.None)).Error?.Code);
        Assert.Equal(requests, fake.Requests.Count);
        Assert.Single(gate.ScheduledDelays);

        // 覆盖写入不受影响
        Assert.True((await store.PutAsync(new ContainerName(Bucket), new BlobKey("c.txt"), Encoding.UTF8.GetBytes("c"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);
    }

    [Fact]
    public async Task BackgroundRetries_DoubleTheDelay_UpToTheHealthProbeInterval()
    {
        var fake = FakeS3Client.Create(Bucket);
        fake.Fault = static request => request == "PutObject" ? new HttpRequestException("Connection refused.") : null;
        var clock = new FakeTimeProvider();
        await using var host = await StartAsync(fake, clock, ("XiHan:ObjectStorage:S3:HealthProbeIntervalSeconds", "10"));
        var gate = host.Services.GetRequiredService<S3ConditionalWriteGate>();

        // 1、2、4、8 秒，之后封顶为健康探测间隔 10 秒；每一步恰好推进这一次的延迟才会排定下一次
        TimeSpan[] expected = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10)];
        for (var index = 0; index < expected.Length; index++)
        {
            await Within(gate.WhenRetryScheduledAsync(index + 1));
            Assert.Equal(expected[index], gate.ScheduledDelays[index]);
            clock.Advance(expected[index]);
        }

        await Within(gate.WhenRetryScheduledAsync(expected.Length + 1));
        Assert.Equal([.. expected, TimeSpan.FromSeconds(10)], gate.ScheduledDelays);
        Assert.False(gate.Settled.IsCompleted);
    }

    [Fact]
    public async Task Stopping_EndsTheBackgroundRetries()
    {
        var fake = FakeS3Client.Create(Bucket);
        fake.Fault = static request => request == "PutObject" ? new HttpRequestException("Connection refused.") : null;
        var clock = new FakeTimeProvider();
        var host = await StartAsync(fake, clock);
        var gate = host.Services.GetRequiredService<S3ConditionalWriteGate>();
        await Within(gate.WhenRetryScheduledAsync(1));

        // 停止后推进时钟不再探测
        await host.DisposeAsync();
        var requests = fake.Requests.Count;
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(requests, fake.Requests.Count);
        Assert.Single(gate.ScheduledDelays);
    }

    [Fact]
    public async Task NamedStore_WhenTheProbeCannotConnect_IsCreated_SurvivesALaterDenial_AndRecovers_ButAnUnsupportedServiceIsNotCreated()
    {
        var reachableLater = FakeS3Client.Create(Bucket);
        reachableLater.Fault = static request => request == "PutObject" ? new HttpRequestException("Connection refused.") : null;
        var ignoring = FakeS3Client.Create(Bucket);
        ignoring.HonoursPutCondition = false;
        var clock = new FakeTimeProvider();
        var provider = new S3BlobStoreProvider(clock, NullLoggerFactory.Instance, options => options.ServiceUrl == "http://ignoring:9000" ? ignoring.AsClient() : reachableLater.AsClient());
        var registry = new BlobStores([provider], clock, NullLoggerFactory.Instance);
        await using (registry)
        {
            // 连不上：照常建出并登记，不覆盖写入不可用，健康探测带出原因
            var store = (await registry.GetOrAddAsync(Definition("media", "http://reachable-later:9000"), CancellationToken.None)).ValueOrThrow();
            Assert.Equal(["media"], registry.Names);
            Assert.Equal(ObjectStorageErrors.ConditionalWriteUnconfirmed, (await Put(store, "a.txt")).Error?.Code);
            Assert.Equal(ObjectStorageErrors.StoreUnavailable, (await provider.ProbeAsync(store, CancellationToken.None)).Error?.Code);

            // 连上时被拒（凭据已被吊销）：实例仍登记着、照常重试，健康探测带出被拒（注册表记为 Degraded）
            var gate = Assert.IsType<S3BlobStore>(store).ConditionalWrites;
            await Within(gate.WhenRetryScheduledAsync(1));
            reachableLater.Fault = static request => request == "PutObject" ? FakeS3Client.ServiceError(HttpStatusCode.Forbidden, "AccessDenied") : null;
            clock.Advance(TimeSpan.FromSeconds(1));
            await Within(gate.WhenRetryScheduledAsync(2));
            Assert.Equal(ObjectStorageErrors.ProbeDenied, (await provider.ProbeAsync(store, CancellationToken.None)).Error?.Code);
            Assert.Equal(["media"], registry.Names);

            // 权限改好后后台重试通过：健康探测恢复，不覆盖写入照常
            reachableLater.Fault = static _ => null;
            clock.Advance(TimeSpan.FromSeconds(2));
            await Within(gate.Settled);
            Assert.True((await provider.ProbeAsync(store, CancellationToken.None)).IsSuccess);
            Assert.True((await Put(store, "a.txt")).IsSuccess);

            // 建实例时就探明不认：不建、不登记
            var unsupported = await registry.GetOrAddAsync(Definition("legacy", "http://ignoring:9000"), CancellationToken.None);
            Assert.Equal(ObjectStorageErrors.ConditionalWriteUnavailable, unsupported.Error?.Code);
            Assert.Equal(["media"], registry.Names);
        }
    }

    [Fact]
    public async Task NamedStore_Released_StopsItsBackgroundRetries()
    {
        var fake = FakeS3Client.Create(Bucket);
        fake.Fault = static request => request == "PutObject" ? new HttpRequestException("Connection refused.") : null;
        var clock = new FakeTimeProvider();
        var registry = new BlobStores([new S3BlobStoreProvider(clock, NullLoggerFactory.Instance, _ => fake.AsClient())], clock, NullLoggerFactory.Instance);
        S3ConditionalWriteGate gate;
        await using (registry)
        {
            gate = Assert.IsType<S3BlobStore>((await registry.GetOrAddAsync(Definition("media", "http://localhost:9000"), CancellationToken.None)).ValueOrThrow()).ConditionalWrites;
            await Within(gate.WhenRetryScheduledAsync(1));
        }

        // 注册表停止时释放实例：重试随之结束，推进时钟不再探测
        var requests = fake.Requests.Count;
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(requests, fake.Requests.Count);
        Assert.Single(gate.ScheduledDelays);
    }

    /// <summary>
    /// 命名实例的定义
    /// </summary>
    private static BlobStoreDefinition Definition(string name, string serviceUrl)
        => new(name, S3BlobStoreProvider.ProviderName, new Dictionary<string, string> { ["ServiceUrl"] = serviceUrl, ["HealthBucket"] = Bucket, ["AccessKey"] = "key", ["SecretKey"] = "secret" });

    /// <summary>
    /// 以不覆盖方式写一个对象
    /// </summary>
    private static Task<Result<BlobMetadata>> Put(IBlobStore store, string key)
        => store.PutAsync(new ContainerName(Bucket), new BlobKey(key), Encoding.UTF8.GetBytes(key), new BlobPutOptions { Overwrite = false }, CancellationToken.None).AsTask();

    /// <summary>
    /// 缺省实例的健康项
    /// </summary>
    private static ContributorStatus Connection(TestHost host)
        => Assert.Single(host.App.Health().Contributions, static c => c.Name == S3ObjectStorageFeature.HealthName);

    /// <summary>
    /// 等待，最多 30 秒（真实时间，只为失败时不挂死）
    /// </summary>
    private static Task Within(Task task) => task.WaitAsync(TimeSpan.FromSeconds(30));

    /// <summary>
    /// 等待结果，最多 30 秒（真实时间，只为失败时不挂死）
    /// </summary>
    private static Task<T> Within<T>(Task<T> task) => task.WaitAsync(TimeSpan.FromSeconds(30));

    /// <summary>
    /// 条件写入状态的日志，按写入顺序
    /// </summary>
    private static List<(LogLevel Level, string Category, string Message)> GateLogs(LogCapture logs)
        => [.. logs.Entries.Where(static e => e.Category == typeof(S3ConditionalWriteGate).FullName)];

    /// <summary>
    /// 以替身为客户端、以假时钟启动只装 S3 的宿主
    /// </summary>
    private static Task<TestHost> StartAsync(FakeS3Client fake, FakeTimeProvider clock, params (string Key, string Value)[] extra)
        => StartAsync(fake, clock, logs: null, extra);

    /// <summary>
    /// 以替身为客户端、以假时钟启动只装 S3 的宿主，日志收进 <paramref name="logs"/>（为空时用缺省日志）
    /// </summary>
    private static Task<TestHost> StartAsync(FakeS3Client fake, FakeTimeProvider clock, LogCapture? logs, params (string Key, string Value)[] extra)
        => TestHost.StartAsync(
            static b => b.UseFeature<S3ObjectStorageFeature>(),
            [
                new("XiHan:ObjectStorage:S3:ServiceUrl", "http://localhost:9000"),
                new("XiHan:ObjectStorage:S3:HealthBucket", Bucket),
                .. extra.Select(static e => new KeyValuePair<string, string?>(e.Key, e.Value)),
            ],
            services =>
            {
                services.AddSingleton(fake.AsClient());
                services.AddSingleton<TimeProvider>(clock);
                if (logs is not null)
                {
                    services.AddSingleton<ILoggerFactory>(logs);
                }
            });
}
