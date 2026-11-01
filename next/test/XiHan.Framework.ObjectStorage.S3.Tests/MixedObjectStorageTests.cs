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
/// 两种装配顺序下，两个存储包的命名实例 Provider 都登记，与谁被选为缺省存储无关；缺省的 <see cref="IBlobStore"/>、<see cref="IBlobMultipartStore"/> 与 <see cref="IBlobUrlSigner"/> 落在同一存储上：被要求的契约选得不一致时装配失败（<see cref="ObjectStorageErrors.StoreSelectionInvalid"/>），只是兜底选出的不失败，那个存储包按被顶替处理（ADR-0082）。不连接 S3。
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
    public async Task FileSystemDefault_OnlyBlobSelected_LeavesS3NamedOnly_WithoutHostedSigner(bool fileSystemFirst)
    {
        // 只把对象存储指定给文件系统：S3 的分片上传与预签名只是兜底选出（没人依赖、没人指定），S3 按被顶替处理
        var (graph, provider) = Build(fileSystemFirst, hostedSigner: false, ("XiHan:Providers:objectstorage.blob", "xihan.objectstorage.filesystem"));
        await using var _ = provider;

        var s3 = graph["xihan.objectstorage.s3"];
        Assert.True(s3.IsSelectedFor("objectstorage.presign"));
        Assert.False(s3.IsRequestedFor("objectstorage.presign"));
        Assert.False(s3.IsRequestedFor("objectstorage.multipart"));
        Assert.IsType<FileSystemBlobStore>(provider.GetRequiredService<IBlobStore>());
        Assert.Null(provider.GetService<IBlobMultipartStore>());
        Assert.Null(provider.GetService<IBlobUrlSigner>());
        Assert.Null(provider.GetService<S3BlobStore>());
        Assert.Null(provider.GetService<S3ObjectStorageOptions>());
        Assert.Equal(["filesystem", "s3"], provider.GetServices<IBlobStoreProvider>().Select(static p => p.Name).Order(StringComparer.Ordinal));
        Assert.Null(provider.GetKeyedService<PeriodicHealthContributor>(S3ObjectStorageFeature.HealthName));

        // 启动与停止：S3 不建客户端、不探测，文件系统照常建根目录
        var app = provider.GetRequiredService<App>();
        await app.StartAsync(CancellationToken.None);
        await app.StopAsync(CancellationToken.None);
        Assert.True(Directory.Exists(Path.Combine(_root, "default")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FileSystemDefault_WithMultipartSelected_KeepsMultipartOnFileSystem(bool fileSystemFirst)
    {
        var (_, provider) = Build(fileSystemFirst, hostedSigner: false, ("XiHan:Providers:objectstorage.blob", "xihan.objectstorage.filesystem"), ("XiHan:Providers:objectstorage.multipart", "xihan.objectstorage.filesystem"));
        using var _ = provider;

        var store = Assert.IsType<FileSystemBlobStore>(provider.GetRequiredService<IBlobStore>());
        Assert.Same(store, provider.GetRequiredService<IBlobMultipartStore>());
        Assert.Null(provider.GetService<IBlobUrlSigner>());
        Assert.Null(provider.GetService<S3BlobStore>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RequestedSplitSelection_FailsAssembly(bool fileSystemFirst)
    {
        // 有特性依赖预签名：预签名解析到 S3 且是被要求的，缺省签名器会签出 S3 的地址
        var required = Assert.Throws<FrameworkException>(() => Build(fileSystemFirst, hostedSigner: false, [new CapabilityConsumerFeature("objectstorage.presign")], ("XiHan:Providers:objectstorage.blob", "xihan.objectstorage.filesystem")));
        Assert.Equal(ObjectStorageErrors.StoreSelectionInvalid, required.Code);
        Assert.Contains("'xihan.objectstorage.s3' is requested for 'objectstorage.presign'", required.Message, StringComparison.Ordinal);

        // 配置把分片上传指定给 S3：同样是被要求的
        var configured = Assert.Throws<FrameworkException>(() => Build(fileSystemFirst, hostedSigner: false, ("XiHan:Providers:objectstorage.blob", "xihan.objectstorage.filesystem"), ("XiHan:Providers:objectstorage.multipart", "xihan.objectstorage.s3")));
        Assert.Equal(ObjectStorageErrors.StoreSelectionInvalid, configured.Code);
        Assert.Contains("'xihan.objectstorage.s3' is requested for 'objectstorage.multipart'", configured.Message, StringComparison.Ordinal);

        // 反过来，缺省存储是 S3 而分片上传指定给文件系统
        var multipart = Assert.Throws<FrameworkException>(() => Build(fileSystemFirst, hostedSigner: false, ("XiHan:Providers:objectstorage.multipart", "xihan.objectstorage.filesystem")));
        Assert.Equal(ObjectStorageErrors.StoreSelectionInvalid, multipart.Code);
        Assert.Contains("'xihan.objectstorage.filesystem' is requested for 'objectstorage.multipart'", multipart.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RequiredPresign_OnFileSystemDefault_UsesHostedSigner_WhenSelected(bool fileSystemFirst)
    {
        // 文件模块依赖预签名、缺省存储是文件系统：装宿主签名器并把预签名指定给它，S3 只留命名实例
        var (graph, provider) = Build(fileSystemFirst, hostedSigner: true, [new CapabilityConsumerFeature("objectstorage.presign")], ("XiHan:Providers:objectstorage.blob", "xihan.objectstorage.filesystem"), ("XiHan:Providers:objectstorage.presign", "xihan.hosting.blob-urls"));
        using var _ = provider;

        Assert.True(graph["xihan.hosting.blob-urls"].IsRequestedFor("objectstorage.presign"));
        Assert.IsType<HostedBlobUrlSigner>(provider.GetRequiredService<IBlobUrlSigner>());
        Assert.IsType<FileSystemBlobStore>(provider.GetRequiredService<IBlobStore>());
        Assert.Null(provider.GetService<IBlobMultipartStore>());
        Assert.Null(provider.GetService<S3BlobStore>());
    }

    /// <summary>
    /// 按给定顺序装配两个存储包（可选再加宿主代收发的签名器），返回图谱与容器。
    /// </summary>
    private (ResolvedGraph Graph, ServiceProvider Provider) Build(bool fileSystemFirst, bool hostedSigner, params (string Key, string Value)[] values)
        => Build(fileSystemFirst, hostedSigner, [], values);

    /// <summary>
    /// 按给定顺序装配两个存储包，可选再加宿主代收发的签名器与别的特性，返回图谱与容器。
    /// </summary>
    private (ResolvedGraph Graph, ServiceProvider Provider) Build(bool fileSystemFirst, bool hostedSigner, IReadOnlyList<IFeature> extra, params (string Key, string Value)[] values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
        [
            new("XiHan:ObjectStorage:S3:ServiceUrl", "http://localhost:9000"),
            new("XiHan:ObjectStorage:S3:AccessKey", "ak"),
            new("XiHan:ObjectStorage:S3:SecretKey", "sk"),
            new("XiHan:ObjectStorage:S3:HealthBucket", "probe"),
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

        foreach (var feature in extra)
        {
            builder.UseFeature(feature);
        }

        var graph = builder.Build();
        return (graph, services.BuildServiceProvider());
    }
}
