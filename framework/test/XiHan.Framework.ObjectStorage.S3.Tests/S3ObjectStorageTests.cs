// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// S3 Provider 独有的行为
/// </summary>
/// <remarks>
/// 选项解析与绑定覆盖、客户端创建、特性在宿主里的装配、桶不存在的语义与 <c>EnsureBuckets</c>。一致性套件覆盖的通用行为不在此重复
/// </remarks>
public sealed class S3ObjectStorageTests
{
    /// <summary>
    /// 用键值对构造内存配置
    /// </summary>
    private static IConfiguration Configuration(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    [Fact]
    public void Options_FromConfiguration_ParsesAndRejectsInvalid()
    {
        var defaults = S3ObjectStorageOptions.FromConfiguration(Configuration());
        Assert.Null(defaults.ServiceUrl);
        Assert.Equal("us-east-1", defaults.Region);
        Assert.Null(defaults.AccessKey);
        Assert.True(defaults.ForcePathStyle);
        Assert.False(defaults.EnsureBuckets);

        var options = S3ObjectStorageOptions.FromConfiguration(Configuration(
            ("XiHan:ObjectStorage:S3:ServiceUrl", "http://localhost:9000"),
            ("XiHan:ObjectStorage:S3:Region", "cn-north-1"),
            ("XiHan:ObjectStorage:S3:AccessKey", "ak"),
            ("XiHan:ObjectStorage:S3:SecretKey", "sk"),
            ("XiHan:ObjectStorage:S3:ForcePathStyle", "false"),
            ("XiHan:ObjectStorage:S3:EnsureBuckets", "true")));
        Assert.Equal("http://localhost:9000", options.ServiceUrl);
        Assert.Equal("cn-north-1", options.Region);
        Assert.Equal(("ak", "sk"), (options.AccessKey, options.SecretKey));
        Assert.False(options.ForcePathStyle);
        Assert.True(options.EnsureBuckets);

        Assert.Throws<ArgumentException>(static () => S3ObjectStorageOptions.FromConfiguration(Configuration(("XiHan:ObjectStorage:S3:ServiceUrl", "localhost:9000"))));
        Assert.Throws<ArgumentException>(static () => S3ObjectStorageOptions.FromConfiguration(Configuration(("XiHan:ObjectStorage:S3:ForcePathStyle", "yes"))));
        Assert.Throws<ArgumentException>(static () => new S3ObjectStorageOptions { Region = " " });
    }

    [Fact]
    public void Options_BindingOverridesConfiguration()
    {
        var binding = new BindingValue(S3ObjectStorageFeature.ConnectionBinding, "blob", "https://s3.example.com", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["region"] = "eu-west-1",
            ["accessKey"] = "bound-ak",
            ["secretKey"] = "bound-sk",
            ["forcePathStyle"] = "false",
        });

        var options = S3ObjectStorageOptions.FromConfiguration(Configuration(("XiHan:ObjectStorage:S3:ServiceUrl", "http://localhost:9000"), ("XiHan:ObjectStorage:S3:AccessKey", "ak")), Maybe.Some(binding));

        Assert.Equal("https://s3.example.com", options.ServiceUrl);
        Assert.Equal("eu-west-1", options.Region);
        Assert.Equal(("bound-ak", "bound-sk"), (options.AccessKey, options.SecretKey));
        Assert.False(options.ForcePathStyle);

        // 空白的属性不算给出，逐项照读配置节；绑定只给凭据时地址照读配置节
        var blank = new BindingValue(S3ObjectStorageFeature.ConnectionBinding, "blob", null, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["region"] = " ",
            ["accessKey"] = "",
            ["secretKey"] = "bound-sk",
        });
        var partial = S3ObjectStorageOptions.FromConfiguration(Configuration(("XiHan:ObjectStorage:S3:ServiceUrl", "http://localhost:9000"), ("XiHan:ObjectStorage:S3:Region", "cn-north-1"), ("XiHan:ObjectStorage:S3:AccessKey", "ak"), ("XiHan:ObjectStorage:S3:SecretKey", "sk")), Maybe.Some(blank));
        Assert.Equal("http://localhost:9000", partial.ServiceUrl);
        Assert.Equal("cn-north-1", partial.Region);
        Assert.Equal(("ak", "bound-sk"), (partial.AccessKey, partial.SecretKey));
    }

    [Fact]
    public void Options_ToString_MasksTheAccessKeyIdAndTheSecretKey()
    {
        // 访问密钥 ID 与访问密钥都是凭据（ADR-0089）：打印出 ***，看得出配了；没配时为空
        var text = new S3ObjectStorageOptions { ServiceUrl = "http://localhost:9000", AccessKey = "AKIAIOSFODNN7EXAMPLE", SecretKey = "wJalrXUtnFEMI" }.ToString();
        Assert.Contains("AccessKey = ***, SecretKey = ***", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AKIAIOSFODNN7EXAMPLE", text, StringComparison.Ordinal);
        Assert.DoesNotContain("wJalrXUtnFEMI", text, StringComparison.Ordinal);
        Assert.Contains("ServiceUrl = http://localhost:9000", text, StringComparison.Ordinal);
        Assert.Contains("AccessKey = , SecretKey = ,", new S3ObjectStorageOptions().ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void CreateClient_TargetsEndpointOrRegion()
    {
        using var compatible = S3ObjectStorageFeature.CreateClient(new S3ObjectStorageOptions { ServiceUrl = "http://localhost:9000", AccessKey = "ak", SecretKey = "sk" });
        var config = Assert.IsType<AmazonS3Config>(compatible.Config);
        Assert.Equal(new Uri("http://localhost:9000"), new Uri(config.ServiceURL));
        Assert.True(config.ForcePathStyle);
        Assert.Equal("us-east-1", config.AuthenticationRegion);

        using var amazon = S3ObjectStorageFeature.CreateClient(new S3ObjectStorageOptions { Region = "ap-southeast-1", AccessKey = "ak", SecretKey = "sk" });
        Assert.Equal("ap-southeast-1", amazon.Config.RegionEndpoint?.SystemName);
    }

    [Fact]
    public async Task Feature_ProvidesBlobStore_AndRoundTrips()
    {
        var configuration = S3TestServer.Configuration();
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<S3ObjectStorageFeature>(), configuration);

        var store = host.Services.GetRequiredService<IBlobStore>();
        Assert.IsType<S3BlobStore>(store);
        Assert.Contains("xihan.objectstorage.s3", host.App.DescribeGraph(), StringComparison.Ordinal);

        S3TestServer.EnsureBucket(host.Services.GetRequiredService<IAmazonS3>(), "xihan-feature");
        var key = new BlobKey(Guid.NewGuid().ToString("N") + "/hello.txt");
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("hello"), writable: false);
        Assert.Equal("text/plain", (await store.PutAsync(new ContainerName("xihan-feature"), key, content, new BlobPutOptions { ContentType = "text/plain" }, CancellationToken.None)).ValueOrThrow().ContentType);
        Assert.Equal("hello", Encoding.UTF8.GetString((await store.ReadAllBytesAsync(new ContainerName("xihan-feature"), key, CancellationToken.None)).ValueOrThrow()));
        Assert.True((await store.DeleteAsync(new ContainerName("xihan-feature"), key, CancellationToken.None)).ValueOrThrow());
    }

    [Fact]
    public async Task MissingBucket_IsNotFoundOrEmpty_UnlessEnsured()
    {
        var client = S3TestServer.Client();
        var bucket = "xihan-missing-" + Guid.NewGuid().ToString("N")[..8];
        var store = new S3BlobStore(client, S3TestServer.Options(), TimeProvider.System, NullLoggerFactory.Instance);
        using var content = new MemoryStream([1, 2, 3], writable: false);

        Assert.Equal(ObjectStorageErrors.BlobNotFound, (await store.GetAsync(new ContainerName(bucket), new BlobKey("a"), CancellationToken.None)).Error?.Code);
        Assert.Equal(ObjectStorageErrors.BlobNotFound, (await store.HeadAsync(new ContainerName(bucket), new BlobKey("a"), CancellationToken.None)).Error?.Code);
        Assert.Empty((await store.ListAsync(new ContainerName(bucket), Abstractions.Listing.BlobQuery.All, CancellationToken.None)).ValueOrThrow().Items);
        Assert.False((await store.DeleteAsync(new ContainerName(bucket), new BlobKey("a"), CancellationToken.None)).ValueOrThrow());
        Assert.Equal(ObjectStorageErrors.StoreUnavailable, (await store.PutAsync(new ContainerName(bucket), new BlobKey("a"), content, BlobPutOptions.Default, CancellationToken.None)).Error?.Code);

        // 开启 EnsureBuckets 后写入自动建桶
        var ensuring = new S3BlobStore(client, S3TestServer.Options(ensureBuckets: true), TimeProvider.System, NullLoggerFactory.Instance);
        content.Position = 0;
        var written = (await ensuring.PutAsync(new ContainerName(bucket), new BlobKey("a"), content, BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();
        Assert.Equal(3, written.Length);
        Assert.True((await ensuring.DeleteAsync(new ContainerName(bucket), new BlobKey("a"), CancellationToken.None)).ValueOrThrow());
        await client.DeleteBucketAsync(bucket);
    }
}
