// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Abstractions.Stores;
using XiHan.Framework.Primitives;
using Xunit;

namespace XiHan.Framework.ObjectStorage.Conformance;

/// <summary>
/// <see cref="IBlobStoreProvider"/> 的一致性套件（命名存储实例）。继承并实现 <see cref="CreateProvider"/>、<see cref="ValidSettings"/> 与 <see cref="OtherValidSettings"/>，全部用例随之执行：
/// 按定义建出可读写、探测通过的实例；未知的设置键被拒绝；经 <see cref="BlobStores"/> 同定义复用、定义变了替换、移除后不存在；定义错误翻译为 <see cref="ObjectStorageErrors.StoreDefinitionInvalid"/>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class BlobStoreProviderConformance
{
    /// <summary>
    /// 用例使用的容器；两组设置建出的实例里它都须可写（不存在时由设置保证会建出，或预先建好）。
    /// </summary>
    protected virtual ContainerName Container => new("conformance");

    /// <summary>
    /// 创建被测 Provider。
    /// </summary>
    protected abstract IBlobStoreProvider CreateProvider();

    /// <summary>
    /// 一组能建出可用实例的设置。
    /// </summary>
    protected abstract IReadOnlyDictionary<string, string> ValidSettings();

    /// <summary>
    /// 另一组能建出可用实例、且与 <see cref="ValidSettings"/> 不同的设置（替换用例用）。
    /// </summary>
    protected abstract IReadOnlyDictionary<string, string> OtherValidSettings();

    /// <summary>
    /// 用例 1：按定义建出的实例能写能读，探测通过；实例持有资源时可以释放。
    /// </summary>
    [Fact]
    public async Task Create_YieldsAWorkingStore_ThatProbesHealthy()
    {
        var provider = CreateProvider();
        var store = await provider.CreateAsync(new BlobStoreDefinition("conformance-create", provider.Name, ValidSettings()), CancellationToken.None).ConfigureAwait(false);
        try
        {
            var key = new BlobKey("provider/" + Guid.NewGuid().ToString("N"));
            Assert.True((await store.PutAsync(Container, key, Encoding.UTF8.GetBytes("named"), BlobPutOptions.Default, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            Assert.Equal("named", Encoding.UTF8.GetString((await store.ReadAllBytesAsync(Container, key, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow()));
            Assert.True((await provider.ProbeAsync(store, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        }
        finally
        {
            await DisposeAsync(store).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 用例 2：未知的设置键抛出 <see cref="ArgumentException"/>（拼错的键不会被静默忽略）。
    /// </summary>
    [Fact]
    public async Task Create_RejectsUnknownSettings()
    {
        var provider = CreateProvider();
        var settings = new Dictionary<string, string>(ValidSettings(), StringComparer.OrdinalIgnoreCase) { ["NoSuchSetting"] = "x" };
        await Assert.ThrowsAsync<ArgumentException>(async () => await provider.CreateAsync(new BlobStoreDefinition("conformance-unknown", provider.Name, settings), CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
    }

    /// <summary>
    /// 用例 3：经注册表同名同定义返回同一实例，定义变了建新实例替换，按名取到的是新的；探测后有该实例的健康项且就绪；移除后取用返回 NOTFOUND、健康项消失。
    /// </summary>
    [Fact]
    public async Task Registry_ReusesReplacesAndRemovesInstances()
    {
        var registry = new BlobStores([CreateProvider()], TimeProvider.System);
        await using (registry.ConfigureAwait(false))
        {
            var provider = registry.Providers.Single();
            var first = (await registry.GetOrAddAsync(new BlobStoreDefinition("conformance-registry", provider, ValidSettings()), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
            var again = (await registry.GetOrAddAsync(new BlobStoreDefinition("conformance-registry", provider.ToUpperInvariant(), ValidSettings()), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
            Assert.Same(first, again);

            var replaced = (await registry.GetOrAddAsync(new BlobStoreDefinition("conformance-registry", provider, OtherValidSettings()), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
            Assert.NotSame(first, replaced);
            Assert.Same(replaced, (await registry.GetAsync("conformance-registry", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            Assert.Equal(["conformance-registry"], registry.Names);

            var key = new BlobKey("registry/" + Guid.NewGuid().ToString("N"));
            Assert.True((await replaced.PutAsync(Container, key, Encoding.UTF8.GetBytes("v"), BlobPutOptions.Default, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            await registry.ProbeAsync(CancellationToken.None).ConfigureAwait(false);
            var status = Assert.Single(registry.Check());
            Assert.Equal((BlobStores.HealthPrefix + "conformance-registry", LifecycleState.Ready), (status.Name, status.State));

            Assert.True(await registry.RemoveAsync("conformance-registry", CancellationToken.None).ConfigureAwait(false));
            Assert.False(await registry.RemoveAsync("conformance-registry", CancellationToken.None).ConfigureAwait(false));
            Assert.Equal(ObjectStorageErrors.StoreNotFound, (await registry.GetAsync("conformance-registry", CancellationToken.None).ConfigureAwait(false)).Error?.Code);
            Assert.Empty(registry.Check());
        }
    }

    /// <summary>
    /// 用例 4：Provider 未登记、设置不合法都返回 <see cref="ObjectStorageErrors.StoreDefinitionInvalid"/>，不登记实例；已有的同名实例不受影响。
    /// </summary>
    [Fact]
    public async Task Registry_TranslatesDefinitionErrors_AndKeepsTheExistingInstance()
    {
        var registry = new BlobStores([CreateProvider()], TimeProvider.System);
        await using (registry.ConfigureAwait(false))
        {
            var provider = registry.Providers.Single();
            var unknownProvider = await registry.GetOrAddAsync(new BlobStoreDefinition("conformance-errors", "no-such-provider", ValidSettings()), CancellationToken.None).ConfigureAwait(false);
            Assert.Equal(ObjectStorageErrors.StoreDefinitionInvalid, unknownProvider.Error?.Code);
            Assert.Empty(registry.Names);

            var existing = (await registry.GetOrAddAsync(new BlobStoreDefinition("conformance-errors", provider, ValidSettings()), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
            var invalid = new Dictionary<string, string>(ValidSettings(), StringComparer.OrdinalIgnoreCase) { ["NoSuchSetting"] = "x" };
            Assert.Equal(ObjectStorageErrors.StoreDefinitionInvalid, (await registry.GetOrAddAsync(new BlobStoreDefinition("conformance-errors", provider, invalid), CancellationToken.None).ConfigureAwait(false)).Error?.Code);
            Assert.Same(existing, (await registry.GetAsync("conformance-errors", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
        }
    }

    /// <summary>
    /// 释放直接从 Provider 建出的实例。
    /// </summary>
    private static async ValueTask DisposeAsync(IBlobStore store)
    {
        switch (store)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }
}
