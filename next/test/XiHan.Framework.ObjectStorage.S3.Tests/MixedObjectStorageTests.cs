// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Hosting.AspNetCore.Blobs;
using XiHan.Framework.Kernel.Capabilities;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Multipart;
using XiHan.Framework.ObjectStorage.Abstractions.Signing;
using XiHan.Framework.ObjectStorage.Abstractions.Stores;
using XiHan.Framework.ObjectStorage.FileSystem;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 文件系统与 S3 混装。
/// </summary>
/// <remarks>
/// 两种装配顺序下，两个存储包的命名实例 Provider 都登记，与谁被选为缺省存储无关；缺省的 <see cref="IBlobStore"/>、<see cref="IBlobMultipartStore"/> 与 <see cref="IBlobUrlSigner"/> 落在同一存储上，选得不一致时装配失败（<see cref="ObjectStorageErrors.StoreSelectionInvalid"/>）。只装配、不启动，不连接 S3。
/// </remarks>
public sealed class MixedObjectStorageTests : IDisposable
{
    /// <summary>
    /// 文件系统存储的临时根目录（缺省实例与命名实例各一个子目录）。
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xihan-mixed-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// 删除临时根目录。
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BothStores_DefaultToS3_AndRegisterBothNamedProviders(bool fileSystemFirst)
    {
        var (graph, provider) = Build(fileSystemFirst, hostedSigner: false);
        await using var _ = provider;

        Assert.True(graph["xihan.objectstorage.filesystem"].IsShadowed);
        var store = Assert.IsType<S3BlobStore>(provider.GetRequiredService<IBlobStore>());
        Assert.Same(store, provider.GetRequiredService<IBlobMultipartStore>());
        Assert.Same(store, provider.GetRequiredService<IBlobUrlSigner>());
        Assert.Equal(["filesystem", "s3"], provider.GetServices<IBlobStoreProvider>().Select(static p => p.Name).Order(StringComparer.Ordinal));
        Assert.Null(provider.GetService<FileSystemBlobStore>());

        // 被顶替的文件系统包照样能建命名实例
        var stores = provider.GetRequiredService<BlobStores>();
        var local = (await stores.GetOrAddAsync(new BlobStoreDefinition("local", FileSystemBlobStoreProvider.ProviderName, new Dictionary<string, string> { ["RootPath"] = Path.Combine(_root, "local") }), CancellationToken.None)).ValueOrThrow();
        Assert.IsType<FileSystemBlobStore>(local);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FileSystemDefault_WithHostedSigner_KeepsAllDefaultsOnFileSystem_AndRegistersS3NamedProvider(bool fileSystemFirst)
    {
        var (graph, provider) = Build(fileSystemFirst, hostedSigner: true, ("XiHan:Providers:objectstorage.blob", "xihan.objectstorage.filesystem"), ("XiHan:Providers:objectstorage.multipart", "xihan.objectstorage.filesystem"), ("XiHan:Providers:objectstorage.presign", "xihan.hosting.blob-urls"));
        using var _ = provider;

        Assert.True(graph["xihan.objectstorage.s3"].IsShadowed);
        var store = Assert.IsType<FileSystemBlobStore>(provider.GetRequiredService<IBlobStore>());
        Assert.Same(store, provider.GetRequiredService<IBlobMultipartStore>());
        Assert.IsType<HostedBlobUrlSigner>(provider.GetRequiredService<IBlobUrlSigner>());
        Assert.Equal(["filesystem", "s3"], provider.GetServices<IBlobStoreProvider>().Select(static p => p.Name).Order(StringComparer.Ordinal));
        Assert.Null(provider.GetService<S3BlobStore>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SplitSelection_FailsAssembly(bool fileSystemFirst)
    {
        // 只把对象存储指定给文件系统：分片上传与预签名仍归 S3，缺省签名器会签出 S3 的地址
        var presign = Assert.Throws<FrameworkException>(() => Build(fileSystemFirst, hostedSigner: false, ("XiHan:Providers:objectstorage.blob", "xihan.objectstorage.filesystem")));
        Assert.Equal(ObjectStorageErrors.StoreSelectionInvalid, presign.Code);
        Assert.Contains("'xihan.objectstorage.s3'", presign.Message, StringComparison.Ordinal);
        Assert.Contains("'objectstorage.multipart', 'objectstorage.presign'", presign.Message, StringComparison.Ordinal);

        // 反过来，缺省存储是 S3 而分片上传指定给文件系统
        var multipart = Assert.Throws<FrameworkException>(() => Build(fileSystemFirst, hostedSigner: false, ("XiHan:Providers:objectstorage.multipart", "xihan.objectstorage.filesystem")));
        Assert.Equal(ObjectStorageErrors.StoreSelectionInvalid, multipart.Code);
        Assert.Contains("'xihan.objectstorage.filesystem'", multipart.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 按给定顺序装配两个存储包（可选再加宿主代收发的签名器），返回图谱与容器。
    /// </summary>
    private (ResolvedGraph Graph, ServiceProvider Provider) Build(bool fileSystemFirst, bool hostedSigner, params (string Key, string Value)[] values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
        [
            new("XiHan:ObjectStorage:S3:ServiceUrl", "http://localhost:9000"),
            new("XiHan:ObjectStorage:S3:AccessKey", "ak"),
            new("XiHan:ObjectStorage:S3:SecretKey", "sk"),
            new("XiHan:ObjectStorage:FileSystem:RootPath", Path.Combine(_root, "default")),
            new("XiHan:ObjectStorage:SignedUrls:Secret", "unit-test-signing-secret-with-32-bytes!"),
            .. values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value)),
        ]).Build();
        var services = new ServiceCollection();
        var builder = new AppBuilder(services, configuration);
        if (fileSystemFirst)
        {
            builder.UseFeature<FileSystemObjectStorageFeature>().UseFeature<S3ObjectStorageFeature>();
        }
        else
        {
            builder.UseFeature<S3ObjectStorageFeature>().UseFeature<FileSystemObjectStorageFeature>();
        }

        if (hostedSigner)
        {
            builder.UseFeature<HostedBlobUrlFeature>();
        }

        var graph = builder.Build();
        return (graph, services.BuildServiceProvider());
    }
}
