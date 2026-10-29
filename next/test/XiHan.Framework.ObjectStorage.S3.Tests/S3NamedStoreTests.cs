// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Signing;
using XiHan.Framework.ObjectStorage.Abstractions.Stores;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// S3 命名实例：配置定义的实例经特性登记的注册表取到，自己签预签名地址（直连存储）；探测按实例的健康桶，桶不存在即 Degraded。设置错误不建实例。未设置 <c>XIHAN_TEST_S3</c> 时跳过。
/// </summary>
public sealed class S3NamedStoreTests
{
    /// <summary>
    /// 用例的桶。
    /// </summary>
    private const string Bucket = "named";

    /// <summary>
    /// 直连存储的客户端：不带任何凭据。
    /// </summary>
    private static readonly HttpClient _http = new();

    [Fact]
    public async Task ConfiguredStores_SignTheirOwnUrls_AndProbeTheirHealthBucket()
    {
        S3TestServer.EnsureBucket(S3TestServer.Client(), Bucket);
        await using var host = await TestHost.StartAsync(
            static app => app.UseFeature<S3ObjectStorageFeature>(),
            [
                .. S3TestServer.Configuration(),
                new("XiHan:ObjectStorage:Stores:media:Provider", "S3"),
                new("XiHan:ObjectStorage:Stores:media:ServiceUrl", S3TestServer.RequireServiceUrl()),
                new("XiHan:ObjectStorage:Stores:media:AccessKey", S3TestServer.AccessKey),
                new("XiHan:ObjectStorage:Stores:media:SecretKey", S3TestServer.SecretKey),
                new("XiHan:ObjectStorage:Stores:media:HealthBucket", Bucket),
                new("XiHan:ObjectStorage:Stores:gone:Provider", "s3"),
                new("XiHan:ObjectStorage:Stores:gone:ServiceUrl", S3TestServer.RequireServiceUrl()),
                new("XiHan:ObjectStorage:Stores:gone:AccessKey", S3TestServer.AccessKey),
                new("XiHan:ObjectStorage:Stores:gone:SecretKey", S3TestServer.SecretKey),
                new("XiHan:ObjectStorage:Stores:gone:HealthBucket", "no-such-bucket-" + Guid.NewGuid().ToString("N")[..8]),
                new("XiHan:ObjectStorage:Stores:typo:Provider", "s3"),
                new("XiHan:ObjectStorage:Stores:typo:SeviceUrl", S3TestServer.RequireServiceUrl()),
            ]);
        var registry = host.Services.GetRequiredService<BlobStores>();
        Assert.Equal("s3", Assert.Single(registry.Providers));

        // 命名实例自己就是签名器：地址直连存储，不经宿主
        var media = (await registry.GetAsync("media", CancellationToken.None)).ValueOrThrow();
        var key = Guid.NewGuid().ToString("N")[..8] + "/clip.txt";
        Assert.True((await media.PutAsync(Bucket, key, Encoding.UTF8.GetBytes("named"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);
        var url = (await Assert.IsAssignableFrom<IBlobUrlSigner>(media).SignAsync(Bucket, key, BlobUrlRequest.ForDownload(), CancellationToken.None)).ValueOrThrow();
        using (var download = await _http.GetAsync(url.Url))
        {
            Assert.Equal((HttpStatusCode.OK, "named"), (download.StatusCode, await download.Content.ReadAsStringAsync()));
        }

        // 健康项：健康桶在即就绪，不在即降级（实例仍可取用）
        _ = (await registry.GetAsync("gone", CancellationToken.None)).ValueOrThrow();
        await registry.ProbeAsync(CancellationToken.None);
        var statuses = registry.Check().ToDictionary(static s => s.Name, static s => s.State);
        Assert.Equal((LifecycleState.Ready, LifecycleState.Degraded), (statuses["objectstorage.store.media"], statuses["objectstorage.store.gone"]));

        // 拼错的设置键：不建实例
        var typo = await registry.GetAsync("typo", CancellationToken.None);
        Assert.Equal(ObjectStorageErrors.StoreDefinitionInvalid, typo.Error?.Code);
        Assert.Contains("SeviceUrl", typo.Error?.Details?["reason"]?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Settings_UseTheConfigurationKeys_AndRejectUnknownOnes()
    {
        var options = S3ObjectStorageOptions.FromSettings(new Dictionary<string, string>
        {
            ["serviceurl"] = "http://localhost:9000",
            ["Region"] = "cn-north-1",
            ["ForcePathStyle"] = "false",
            ["EnsureBuckets"] = "true",
        });
        Assert.Equal(("http://localhost:9000", "cn-north-1", false, true), (options.ServiceUrl, options.Region, options.ForcePathStyle, options.EnsureBuckets));
        Assert.Throws<ArgumentException>(static () => S3ObjectStorageOptions.FromSettings(new Dictionary<string, string> { ["HealthProbeIntervalSeconds"] = "5" }));
        Assert.Throws<ArgumentException>(static () => S3ObjectStorageOptions.FromSettings(new Dictionary<string, string> { ["ForcePathStyle"] = "yes" }));
        Assert.Throws<ArgumentException>(static () => S3ObjectStorageOptions.FromSettings(new Dictionary<string, string> { ["ServiceUrl"] = "ftp://example.com" }));
    }
}
