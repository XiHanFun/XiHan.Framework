// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Abstractions.Listing;
using XiHan.Framework.ObjectStorage.Abstractions.Stores;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.ObjectStorage.FileSystem.Tests;

/// <summary>
/// 命名存储实例的注册表：配置定义的实例首次取用时建出、设置错误、健康项随实例的状态、替换 / 移除 / 停止时释放实例，以及特性装配后注册表与健康项可用。
/// </summary>
public sealed class BlobStoresTests : IDisposable
{
    /// <summary>
    /// 临时根目录。
    /// </summary>
    private readonly TemporaryRoot _root = new();

    /// <summary>
    /// 删除临时根目录。
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
        var registry = new BlobStores([new FileSystemBlobStoreProvider(TimeProvider.System)], TimeProvider.System, configuration);
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
        Assert.Throws<ArgumentException>(() => new BlobStores([new FileSystemBlobStoreProvider(TimeProvider.System), new FileSystemBlobStoreProvider(TimeProvider.System)], TimeProvider.System));
    }

    [Fact]
    public async Task Health_FollowsEachInstance_AndQuotaExhaustionDegrades()
    {
        var registry = new BlobStores([new FileSystemBlobStoreProvider(TimeProvider.System)], TimeProvider.System);
        await using (registry)
        {
            var tiny = (await registry.GetOrAddAsync(Definition("tiny", ("RootPath", Path.Combine(_root.Path, "tiny")), ("MaxTotalBytes", "4")), CancellationToken.None)).ValueOrThrow();
            _ = (await registry.GetOrAddAsync(Definition("roomy", ("RootPath", Path.Combine(_root.Path, "roomy"))), CancellationToken.None)).ValueOrThrow();
            await registry.ProbeAsync(CancellationToken.None);
            Assert.Equal([("objectstorage.store.roomy", LifecycleState.Ready), ("objectstorage.store.tiny", LifecycleState.Ready)], registry.Check().Select(static s => (s.Name, s.State)));

            Assert.True((await tiny.PutAsync("docs", "full.bin", Encoding.UTF8.GetBytes("1234"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);
            await registry.ProbeAsync(CancellationToken.None);
            var degraded = registry.Check().Single(static s => s.Name == "objectstorage.store.tiny");
            Assert.Equal((LifecycleState.Degraded, ObjectStorageErrors.QuotaExceeded), (degraded.State, degraded.Error?.Code));
        }
    }

    [Fact]
    public async Task Instances_AreDisposedWhenReplacedRemovedOrStopped()
    {
        var provider = new CountingProvider();
        var registry = new BlobStores([provider], TimeProvider.System);
        CountingStore first, second, third;
        await using (registry)
        {
            first = (CountingStore)(await registry.GetOrAddAsync(Counting("one", "1"), CancellationToken.None)).ValueOrThrow();
            second = (CountingStore)(await registry.GetOrAddAsync(Counting("one", "2"), CancellationToken.None)).ValueOrThrow();
            Assert.Equal((1, 0), (first.Disposed, second.Disposed));

            Assert.True(await registry.RemoveAsync("one", CancellationToken.None));
            Assert.Equal(1, second.Disposed);

            third = (CountingStore)(await registry.GetOrAddAsync(Counting("two", "1"), CancellationToken.None)).ValueOrThrow();
            Assert.Equal(0, third.Disposed);
        }

        // 停止时释放剩下的；重复停止不再释放
        await registry.DisposeAsync();
        Assert.Equal((1, 1, 1), (first.Disposed, second.Disposed, third.Disposed));
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
            ]);
        var registry = host.Services.GetRequiredService<BlobStores>();
        Assert.Equal("filesystem", Assert.Single(registry.Providers));
        var archive = (await registry.GetAsync("archive", CancellationToken.None)).ValueOrThrow();
        Assert.NotSame(host.Services.GetRequiredService<IBlobStore>(), archive);
        await registry.ProbeAsync(CancellationToken.None);
        Assert.Contains(host.App.Health().Contributions, static c => c.Name == "objectstorage.store.archive" && c.State == LifecycleState.Ready);
    }

    /// <summary>
    /// 内存配置。
    /// </summary>
    private static IConfiguration Configuration(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    /// <summary>
    /// 文件系统 Provider 的定义。
    /// </summary>
    private static BlobStoreDefinition Definition(string name, params (string Key, string Value)[] settings)
        => new(name, FileSystemBlobStoreProvider.ProviderName, settings.ToDictionary(static s => s.Key, static s => s.Value));

    /// <summary>
    /// 计数 Provider 的定义：版本不同即定义不同。
    /// </summary>
    private static BlobStoreDefinition Counting(string name, string version)
        => new(name, "counting", new Dictionary<string, string> { ["Version"] = version });

    /// <summary>
    /// 记录释放次数的 Provider：设置原样接受，探测总是成功。
    /// </summary>
    private sealed class CountingProvider : IBlobStoreProvider
    {
        /// <inheritdoc />
        public string Name => "counting";

        /// <inheritdoc />
        public ValueTask<IBlobStore> CreateAsync(BlobStoreDefinition definition, CancellationToken cancellationToken) => ValueTask.FromResult<IBlobStore>(new CountingStore());

        /// <inheritdoc />
        public ValueTask<Result> ProbeAsync(IBlobStore store, CancellationToken cancellationToken) => ValueTask.FromResult(Result.Success());
    }

    /// <summary>
    /// 只记录释放次数的存储；读写不会被调用。
    /// </summary>
    private sealed class CountingStore : IBlobStore, IDisposable
    {
        /// <summary>
        /// 释放次数。
        /// </summary>
        public int Disposed { get; private set; }

        /// <inheritdoc />
        public void Dispose() => Disposed++;

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<BlobPutResult>> PutAsync(ContainerName container, IReadOnlyList<BlobUpload> uploads, CancellationToken cancellationToken) => throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<Result<BlobContent>> GetAsync(ContainerName container, BlobKey key, CancellationToken cancellationToken) => throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<Result<BlobContent>> GetAsync(ContainerName container, BlobKey key, BlobRange range, CancellationToken cancellationToken) => throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<Result<BlobMetadata>> HeadAsync(ContainerName container, BlobKey key, CancellationToken cancellationToken) => throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<BlobDeleteResult>> DeleteAsync(ContainerName container, IReadOnlyList<BlobKey> keys, CancellationToken cancellationToken) => throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<Result<BlobPage>> ListAsync(ContainerName container, BlobQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
