// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.ObjectStorage.FileSystem.Tests;

/// <summary>
/// 健康项 <c>objectstorage.filesystem.capacity</c>：宿主启动后 Ready，已用量达到总配额后 Degraded 并带 <see cref="ObjectStorageErrors.QuotaExceeded"/>。
/// </summary>
public sealed class FileSystemObjectStorageHealthTests : IDisposable
{
    /// <summary>
    /// 本用例的临时根目录。
    /// </summary>
    private readonly TemporaryRoot _root = new();

    /// <inheritdoc />
    public void Dispose() => _root.Dispose();

    [Fact]
    public async Task Health_IsReadyUntilTheQuotaIsExhausted_ThenDegraded()
    {
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<FileSystemObjectStorageFeature>(),
            [new("XiHan:ObjectStorage:FileSystem:RootPath", _root.Path), new("XiHan:ObjectStorage:FileSystem:MaxTotalBytes", "3")]);

        var ready = Assert.Single(host.App.Health().Contributions, static c => c.Name == FileSystemObjectStorageFeature.HealthName);
        Assert.Equal(LifecycleState.Ready, ready.State);

        var store = host.Services.GetRequiredService<IBlobStore>();
        Assert.True((await store.PutAsync("hosted", "k", Encoding.UTF8.GetBytes("abc"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);

        var health = host.App.Health();
        var degraded = Assert.Single(health.Contributions, static c => c.Name == FileSystemObjectStorageFeature.HealthName);
        Assert.Equal(LifecycleState.Degraded, degraded.State);
        Assert.Equal(ObjectStorageErrors.QuotaExceeded, degraded.Error?.Code);
        Assert.Equal(ObjectStorageMessages.QuotaReached, degraded.Error?.MessageKey);
        Assert.Equal(3L, degraded.Error?.Details?["limit"]);
        Assert.Equal(3L, degraded.Error?.Details?["used"]);
        Assert.True(health.Ready);
    }
}
