// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Abstractions.Listing;
using XiHan.Framework.ObjectStorage.Abstractions.Stores;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.ObjectStorage.FileSystem.Tests;

/// <summary>
/// 命名存储实例的注册表
/// </summary>
/// <remarks>
/// 配置定义的实例首次取用时建出、设置错误、健康项随实例的状态、替换 / 移除 / 停止时释放实例、批量移除的逐项结果、登记数上限（ADR-0266），以及特性装配后注册表与健康项可用
/// </remarks>
public sealed class BlobStoresTests : IDisposable
{
    /// <summary>
    /// 临时根目录
    /// </summary>
    private readonly TemporaryRoot _root = new();

    /// <summary>
    /// 删除临时根目录
    /// </summary>
    public void Dispose() => _root.Dispose();

    [Fact]
    public async Task ConfiguredStores_AreCreatedOnFirstUse_AndReportDefinitionErrors()
    {
        var configuration = Configuration(
            ("XiHan:ObjectStorage:Stores:archive:Provider", "filesystem"),
            ("XiHan:ObjectStorage:Stores:archive:RootPath", Path.Combine(_root.Path, "archive")),
            ("XiHan:ObjectStorage:Stores:broken:Provider", "filesystem"),
            ("XiHan:ObjectStorage:Stores:broken:RootPath", Path.Combine(_root.Path, "broken")),
            ("XiHan:ObjectStorage:Stores:broken:MaxTotalBytes", "lots"),
            ("XiHan:ObjectStorage:Stores:Invalid_Name:Provider", "filesystem"));
        var registry = new BlobStores([new FileSystemBlobStoreProvider(TimeProvider.System, NullLoggerFactory.Instance)], TimeProvider.System, NullLoggerFactory.Instance, configuration);
        await using (registry)
        {
            // 名字不合法的配置节不算实例
            Assert.Equal(["archive", "broken"], registry.Names);
            var archive = (await registry.GetAsync("archive", CancellationToken.None)).ValueOrThrow();
            Assert.Same(archive, (await registry.GetAsync("archive", CancellationToken.None)).ValueOrThrow());
            Assert.Equal(Path.Combine(_root.Path, "archive"), Assert.IsType<FileSystemBlobStore>(archive).RootPath);

            var broken = await registry.GetAsync("broken", CancellationToken.None);
            Assert.Equal(ObjectStorageErrors.StoreDefinitionInvalid, broken.Error?.Code);
            Assert.Contains("MaxTotalBytes", broken.Error?.Details?["reason"]?.ToString(), StringComparison.Ordinal);
            Assert.Equal(ObjectStorageErrors.StoreNotFound, (await registry.GetAsync("missing", CancellationToken.None)).Error?.Code);
            Assert.Equal(ObjectStorageErrors.StoreNotFound, (await registry.GetAsync("Invalid_Name", CancellationToken.None)).Error?.Code);

            // 命名实例必须给根目录：不给就会和缺省实例落到同一个目录
            var noRoot = await registry.GetOrAddAsync(new BlobStoreDefinition("no-root", "filesystem", new Dictionary<string, string>()), CancellationToken.None);
            Assert.Equal(ObjectStorageErrors.StoreDefinitionInvalid, noRoot.Error?.Code);
        }

        Assert.Throws<ArgumentException>(static () => new BlobStoreDefinition("Upper", "filesystem", new Dictionary<string, string>()));
        Assert.Throws<ArgumentException>(() => new BlobStores([new FileSystemBlobStoreProvider(TimeProvider.System, NullLoggerFactory.Instance), new FileSystemBlobStoreProvider(TimeProvider.System, NullLoggerFactory.Instance)], TimeProvider.System, NullLoggerFactory.Instance));
    }

    [Fact]
    public async Task Health_FollowsEachInstance_AndQuotaExhaustionDegrades()
    {
        var registry = new BlobStores([new FileSystemBlobStoreProvider(TimeProvider.System, NullLoggerFactory.Instance)], TimeProvider.System, NullLoggerFactory.Instance);
        await using (registry)
        {
            var tiny = (await registry.GetOrAddAsync(Definition("tiny", ("RootPath", Path.Combine(_root.Path, "tiny")), ("MaxTotalBytes", "4")), CancellationToken.None)).ValueOrThrow();
            _ = (await registry.GetOrAddAsync(Definition("roomy", ("RootPath", Path.Combine(_root.Path, "roomy"))), CancellationToken.None)).ValueOrThrow();
            await registry.ProbeAsync(CancellationToken.None);
            Assert.Equal([("objectstorage.store.roomy", LifecycleState.Ready), ("objectstorage.store.tiny", LifecycleState.Ready)], registry.Check().Select(static s => (s.Name, s.State)));

            Assert.True((await tiny.PutAsync(new ContainerName("docs"), new BlobKey("full.bin"), Encoding.UTF8.GetBytes("1234"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);
            await registry.ProbeAsync(CancellationToken.None);
            var degraded = registry.Check().Single(static s => s.Name == "objectstorage.store.tiny");
            Assert.Equal((LifecycleState.Degraded, ObjectStorageErrors.QuotaExceeded), (degraded.State, degraded.Error?.Code));
        }
    }

    [Fact]
    public async Task CreateAndProbeFailures_ReportOnlyTheExceptionType_AndLogTheFullException()
    {
        const string secret = "C:/data/blobs/tenant-a";
        var logs = new LogCapture();
        // Provider 以结果报告建不出实例（ADR-0150）：注册表原样交给调用方，并记一条带错误码的 Warning
        var provider = new FaultingProvider { CreateFailure = BlobStoreFailures.Unavailable(new IOException($"Access to the path '{secret}' is denied.")) };
        await using var registry = new BlobStores([provider], TimeProvider.System, logs);

        var refused = await registry.GetOrAddAsync(Counting("one", "1"), CancellationToken.None);
        Assert.Equal(ObjectStorageErrors.StoreUnavailable, refused.Error?.Code);
        Assert.Equal(typeof(IOException).FullName, refused.Error?.Details?["reason"]);
        Assert.DoesNotContain(secret, refused.Error?.Message ?? string.Empty, StringComparison.Ordinal);
        var created = Assert.Single(logs.Entries, static entry => entry.Level >= LogLevel.Warning);
        Assert.Equal(LogLevel.Warning, created.Level);

        // 探测持续抛出：第一次 Warning，之后 Debug；健康快照里只有类型
        provider.CreateFailure = null;
        provider.ProbeFault = new IOException($"The device behind '{secret}' is not ready.");
        (await registry.GetOrAddAsync(Counting("one", "1"), CancellationToken.None)).ValueOrThrow();
        await registry.ProbeAsync(CancellationToken.None);
        await registry.ProbeAsync(CancellationToken.None);

        var status = Assert.Single(registry.Check());
        Assert.Equal(LifecycleState.Degraded, status.State);
        Assert.Equal(typeof(IOException).FullName, status.Error?.Details?["reason"]);
        var probed = logs.Entries.Where(entry => ReferenceEquals(entry.Exception, provider.ProbeFault)).Select(static entry => entry.Level).ToList();
        Assert.Contains(LogLevel.Warning, probed);
        Assert.Contains(LogLevel.Debug, probed);
    }

    [Fact]
    public async Task Instances_AreDisposedWhenReplacedRemovedOrStopped()
    {
        var provider = new CountingProvider();
        var registry = new BlobStores([provider], TimeProvider.System, NullLoggerFactory.Instance);
        CountingStore first, second, third;
        await using (registry)
        {
            first = (CountingStore)(await registry.GetOrAddAsync(Counting("one", "1"), CancellationToken.None)).ValueOrThrow();
            second = (CountingStore)(await registry.GetOrAddAsync(Counting("one", "2"), CancellationToken.None)).ValueOrThrow();
            Assert.Equal((1, 0), (first.Disposed, second.Disposed));

            Assert.Equal([true], await registry.RemoveAsync(["one"], CancellationToken.None));
            Assert.Equal(1, second.Disposed);

            third = (CountingStore)(await registry.GetOrAddAsync(Counting("two", "1"), CancellationToken.None)).ValueOrThrow();
            Assert.Equal(0, third.Disposed);
        }

        // 停止时释放剩下的；重复停止不再释放
        await registry.DisposeAsync();
        Assert.Equal((1, 1, 1), (first.Disposed, second.Disposed, third.Disposed));
    }

    [Fact]
    public async Task Remove_IsBatch_ReportsEachName_AndDisposesTheRemoved()
    {
        await using var registry = new BlobStores([new CountingProvider()], TimeProvider.System, NullLoggerFactory.Instance);
        var one = (CountingStore)(await registry.GetOrAddAsync(Counting("one", "1"), CancellationToken.None)).ValueOrThrow();
        var two = (CountingStore)(await registry.GetOrAddAsync(Counting("two", "1"), CancellationToken.None)).ValueOrThrow();
        var kept = (CountingStore)(await registry.GetOrAddAsync(Counting("kept", "1"), CancellationToken.None)).ValueOrThrow();

        // 逐项结果与入参同序：没登记过的是 false，同一批里重复的名字第二次已不在
        var removed = await registry.RemoveAsync(["one", "missing", "two", "one"], CancellationToken.None);
        var empty = await registry.RemoveAsync([], CancellationToken.None);

        Assert.Equal([true, false, true, false], removed);
        Assert.Empty(empty);
        Assert.Equal((1, 1, 0), (one.Disposed, two.Disposed, kept.Disposed));
        Assert.Equal(["kept"], registry.Names);
        Assert.Equal(ObjectStorageErrors.StoreNotFound, (await registry.GetAsync("one", CancellationToken.None)).Error?.Code);
        Assert.Equal(["objectstorage.store.kept"], registry.Check().Select(static s => s.Name));
    }

    [Fact]
    public async Task Remove_RejectsNullNamesAndCancellation_WithoutRemovingAnything()
    {
        await using var registry = new BlobStores([new CountingProvider()], TimeProvider.System, NullLoggerFactory.Instance);
        var one = (CountingStore)(await registry.GetOrAddAsync(Counting("one", "1"), CancellationToken.None)).ValueOrThrow();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        // 名字为 null 是编程错误：在移除任何实例之前抛出，排在它前面的名字也不移除
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await registry.RemoveAsync(["one", null!], CancellationToken.None));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await registry.RemoveAsync(["one"], cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await registry.RemoveAsync([], cancelled.Token));

        Assert.Equal(0, one.Disposed);
        Assert.Equal(["one"], registry.Names);
    }

    [Fact]
    public async Task Remove_WhenDisposingOneInstanceFails_StillDisposesTheRest_AndLogsIt()
    {
        var logs = new LogCapture();
        await using var registry = new BlobStores([new CountingProvider()], TimeProvider.System, logs);
        var faulty = (CountingStore)(await registry.GetOrAddAsync(Counting("faulty", "1"), CancellationToken.None)).ValueOrThrow();
        var healthy = (CountingStore)(await registry.GetOrAddAsync(Counting("healthy", "1"), CancellationToken.None)).ValueOrThrow();
        faulty.DisposeFault = new IOException("The connection pool is already closed.");

        var removed = await registry.RemoveAsync(["faulty", "healthy"], CancellationToken.None);

        // 两个都已经不在登记表里：前一个释放失败不改变结果，也不挡住后一个的释放
        Assert.Equal([true, true], removed);
        Assert.Equal((1, 1), (faulty.Disposed, healthy.Disposed));
        Assert.Empty(registry.Names);
        var logged = Assert.Single(logs.Entries, entry => ReferenceEquals(entry.Exception, faulty.DisposeFault));
        Assert.Equal(LogLevel.Warning, logged.Level);
        Assert.Contains("'faulty'", logged.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetOrAdd_BeyondCapacity_IsUnavailable_ButReplacingAnExistingNameIsAllowed()
    {
        var provider = new CountingProvider();
        await using var registry = Bounded(provider, 2);
        var first = (CountingStore)(await registry.GetOrAddAsync(Counting("a", "1"), CancellationToken.None)).ValueOrThrow();
        (await registry.GetOrAddAsync(Counting("b", "1"), CancellationToken.None)).ValueOrThrow();

        var full = await registry.GetOrAddAsync(Counting("c", "1"), CancellationToken.None);
        var same = await registry.GetOrAddAsync(Counting("a", "1"), CancellationToken.None);
        var replaced = await registry.GetOrAddAsync(Counting("b", "2"), CancellationToken.None);

        Assert.Equal(2, registry.MaxCount);
        Assert.Equal((ObjectStorageErrors.StoreCapacityExceeded, ObjectStorageMessages.StoreCapacityExceeded), (full.Error?.Code, full.Error?.MessageKey));
        Assert.Equal<object?>("c", full.Error!.Details!["store"]);
        Assert.Equal<object?>(2, full.Error.Details["capacity"]);
        // 满了不淘汰已有的：同名同定义照常返回，同名换定义照常替换，被拒的那次没有建实例
        Assert.Same(first, same.ValueOrThrow());
        Assert.True(replaced.IsSuccess, replaced.ToString());
        Assert.Equal(0, first.Disposed);
        Assert.Equal(["a", "b"], registry.Names);
        Assert.Equal(3, provider.Created);
    }

    [Fact]
    public async Task Remove_FreesASlot_ForANewName()
    {
        await using var registry = Bounded(new CountingProvider(), 1);
        (await registry.GetOrAddAsync(Counting("a", "1"), CancellationToken.None)).ValueOrThrow();
        Assert.Equal(ObjectStorageErrors.StoreCapacityExceeded, (await registry.GetOrAddAsync(Counting("b", "1"), CancellationToken.None)).Error?.Code);

        Assert.Equal([true], await registry.RemoveAsync(["a"], CancellationToken.None));

        Assert.True((await registry.GetOrAddAsync(Counting("b", "1"), CancellationToken.None)).IsSuccess);
        Assert.Equal(["b"], registry.Names);
    }

    [Fact]
    public async Task ConcurrentRegistrations_NeverExceedTheCapacity()
    {
        const int capacity = 4;
        const int callers = 32;
        // 建实例让出线程：登记方在「判容量」与「放进登记表」之间交错
        var provider = new CountingProvider { Yields = true };
        await using var registry = Bounded(provider, capacity);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registrations = Enumerable.Range(0, callers).Select(index => Task.Run(async () =>
        {
            await start.Task;
            return await registry.GetOrAddAsync(Counting("store-" + index, "1"), CancellationToken.None);
        })).ToArray();
        start.SetResult();
        var results = await Task.WhenAll(registrations);

        Assert.Equal(capacity, results.Count(static r => r.IsSuccess));
        Assert.All(results.Where(static r => r.IsFailure), static r => Assert.Equal(ObjectStorageErrors.StoreCapacityExceeded, r.Error?.Code));
        Assert.Equal(capacity, registry.Names.Count);
        Assert.Equal(capacity, registry.Check().Count);
        // 被拒的登记方没有建出实例：没有建了又丢、没人释放的实例
        Assert.Equal(capacity, provider.Created);
    }

    [Fact]
    public async Task ConfiguredStores_CountTowardsTheCapacity_OnceCreated()
    {
        var configuration = Configuration(
            ("XiHan:ObjectStorage:StoreMaxCount", "1"),
            ("XiHan:ObjectStorage:Stores:archive:Provider", "counting"),
            ("XiHan:ObjectStorage:Stores:archive:Version", "1"),
            ("XiHan:ObjectStorage:Stores:reports:Provider", "counting"),
            ("XiHan:ObjectStorage:Stores:reports:Version", "1"));
        await using var registry = new BlobStores([new CountingProvider()], TimeProvider.System, NullLoggerFactory.Instance, configuration);

        // 配置里定义的实例首次取用时才建出，建出之后同样占名额
        Assert.Equal(["archive", "reports"], registry.Names);
        (await registry.GetAsync("archive", CancellationToken.None)).ValueOrThrow();
        var full = await registry.GetAsync("reports", CancellationToken.None);
        Assert.Equal(ObjectStorageErrors.StoreCapacityExceeded, full.Error?.Code);

        Assert.Equal([true], await registry.RemoveAsync(["archive"], CancellationToken.None));
        Assert.True((await registry.GetAsync("reports", CancellationToken.None)).IsSuccess);
    }

    [Fact]
    public async Task MaxCount_DefaultsTo64_AndRejectsInvalidConfiguration()
    {
        await using var unconfigured = new BlobStores([], TimeProvider.System, NullLoggerFactory.Instance);
        await using var largest = Bounded(new CountingProvider(), BlobStores.MaxCountLimit);
        Assert.Equal(64, unconfigured.MaxCount);
        Assert.Equal(1024, largest.MaxCount);

        // 写了值就必须合法：越界、不是整数、空串都在构造时抛出，消息带完整的配置键
        foreach (var value in new[] { "0", "1025", "many", "" })
        {
            var exception = Assert.Throws<ArgumentException>(() => new BlobStores([], TimeProvider.System, NullLoggerFactory.Instance, Configuration(("XiHan:ObjectStorage:StoreMaxCount", value))));
            Assert.Contains("XiHan:ObjectStorage:StoreMaxCount", exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Feature_RegistersTheRegistry_AndItsHealthItems()
    {
        await using var host = await TestHost.StartAsync(
            static app => app.UseFeature<FileSystemObjectStorageFeature>(),
            [
                new("XiHan:ObjectStorage:FileSystem:RootPath", Path.Combine(_root.Path, "default")),
                new("XiHan:ObjectStorage:Stores:archive:Provider", "filesystem"),
                new("XiHan:ObjectStorage:Stores:archive:RootPath", Path.Combine(_root.Path, "archive")),
                new("XiHan:ObjectStorage:StoreMaxCount", "3"),
            ]);
        var registry = host.Services.GetRequiredService<BlobStores>();
        Assert.Equal("filesystem", Assert.Single(registry.Providers));
        Assert.Equal(3, registry.MaxCount);
        var archive = (await registry.GetAsync("archive", CancellationToken.None)).ValueOrThrow();
        Assert.NotSame(host.Services.GetRequiredService<IBlobStore>(), archive);
        await registry.ProbeAsync(CancellationToken.None);
        Assert.Contains(host.App.Health().Contributions, static c => c.Name == "objectstorage.store.archive" && c.State == LifecycleState.Ready);
    }

    /// <summary>
    /// 内存配置
    /// </summary>
    private static IConfiguration Configuration(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    /// <summary>
    /// 文件系统 Provider 的定义
    /// </summary>
    private static BlobStoreDefinition Definition(string name, params (string Key, string Value)[] settings)
        => new(name, FileSystemBlobStoreProvider.ProviderName, settings.ToDictionary(static s => s.Key, static s => s.Value));

    /// <summary>
    /// 以给定的登记数上限建注册表
    /// </summary>
    private static BlobStores Bounded(IBlobStoreProvider provider, int maxCount)
        => new([provider], TimeProvider.System, NullLoggerFactory.Instance, Configuration(("XiHan:ObjectStorage:StoreMaxCount", maxCount.ToString(CultureInfo.InvariantCulture))));

    /// <summary>
    /// 计数 Provider 的定义：版本不同即定义不同
    /// </summary>
    private static BlobStoreDefinition Counting(string name, string version)
        => new(name, "counting", new Dictionary<string, string> { ["Version"] = version });

    /// <summary>
    /// 按脚本抛出的 Provider
    /// </summary>
    private sealed class FaultingProvider : IBlobStoreProvider
    {
        /// <summary>
        /// 建实例时返回的失败
        /// </summary>
        public Error? CreateFailure { get; set; }

        /// <summary>
        /// 探测时抛出的异常
        /// </summary>
        public Exception? ProbeFault { get; set; }

        /// <inheritdoc />
        public string Name => "counting";

        /// <inheritdoc />
        public ValueTask<Result<IBlobStore>> CreateAsync(BlobStoreDefinition definition, CancellationToken cancellationToken)
            => ValueTask.FromResult(CreateFailure is { } failure ? Result.Failure<IBlobStore>(failure) : Result.Success<IBlobStore>(new CountingStore()));

        /// <inheritdoc />
        public ValueTask<Result> ProbeAsync(IBlobStore store, CancellationToken cancellationToken)
            => ProbeFault is { } fault ? throw fault : ValueTask.FromResult(Result.Success());
    }

    /// <summary>
    /// 记录释放次数的 Provider：设置原样接受，探测总是成功
    /// </summary>
    private sealed class CountingProvider : IBlobStoreProvider
    {
        /// <summary>
        /// 建过几个实例
        /// </summary>
        private int _created;

        /// <summary>
        /// 建过几个实例
        /// </summary>
        public int Created => Volatile.Read(ref _created);

        /// <summary>
        /// 建实例之前是否让出线程
        /// </summary>
        public bool Yields { get; init; }

        /// <inheritdoc />
        public string Name => "counting";

        /// <inheritdoc />
        public async ValueTask<Result<IBlobStore>> CreateAsync(BlobStoreDefinition definition, CancellationToken cancellationToken)
        {
            if (Yields)
            {
                await Task.Yield();
            }

            Interlocked.Increment(ref _created);
            return Result.Success<IBlobStore>(new CountingStore());
        }

        /// <inheritdoc />
        public ValueTask<Result> ProbeAsync(IBlobStore store, CancellationToken cancellationToken) => ValueTask.FromResult(Result.Success());
    }

    /// <summary>
    /// 只记录释放次数的存储
    /// </summary>
    /// <remarks>
    /// 读写不会被调用
    /// </remarks>
    private sealed class CountingStore : IBlobStore, IDisposable
    {
        /// <summary>
        /// 释放次数
        /// </summary>
        public int Disposed { get; private set; }

        /// <summary>
        /// 非空时释放以它抛出
        /// </summary>
        public Exception? DisposeFault { get; set; }

        /// <inheritdoc />
        public void Dispose()
        {
            Disposed++;
            if (DisposeFault is { } fault)
            {
                throw fault;
            }
        }

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<Result<BlobMetadata>>> PutAsync(ContainerName container, IReadOnlyList<BlobUpload> uploads, CancellationToken cancellationToken) => throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<Result<BlobContent>> GetAsync(ContainerName container, BlobKey key, CancellationToken cancellationToken) => throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<Result<BlobContent>> GetAsync(ContainerName container, BlobKey key, BlobRange range, CancellationToken cancellationToken) => throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<Result<BlobMetadata>> HeadAsync(ContainerName container, BlobKey key, CancellationToken cancellationToken) => throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<Result<bool>>> DeleteAsync(ContainerName container, IReadOnlyList<BlobKey> keys, CancellationToken cancellationToken) => throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<Result<BlobPage>> ListAsync(ContainerName container, BlobQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
