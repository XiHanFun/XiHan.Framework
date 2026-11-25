// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Abstractions.Multipart;
using XiHan.Framework.ObjectStorage.Abstractions.Stores;
using XiHan.Framework.Testing;
using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// S3 上的内容类型：写得出原值的原样写到线上，写不出的不发请求（ADR-0295）
/// </summary>
/// <remarks>
/// 走真实的 SDK 与套接字，对端是本机回环上的 <see cref="RecordingHttpListener"/>，不需要 S3 服务。AWS SDK for .NET 把 <c>Content-Type</c> 交给 .NET 的 <c>MediaTypeHeaderValue</c> 重新写出：解析不了的在请求发出一半时抛出，写法不同的线上与签名对不上；Provider 原样存取，所以这两类在发请求之前以 <see cref="ObjectStorageErrors.ContentTypeUnsupported"/> 拒绝。缺省实例的传输与命名实例的传输各走一遍
/// </remarks>
public sealed class S3ContentTypeWireTests
{
    /// <summary>
    /// 用例写入的内容
    /// </summary>
    private static readonly byte[] _content = [1, 2, 3];

    /// <summary>
    /// 写入成功时对端回的 ETag
    /// </summary>
    /// <remarks>
    /// SDK 拿它核对内容的 MD5：这是 <see cref="_content"/> 的 MD5
    /// </remarks>
    private const string ContentETag = "5289df737df57326fcdd22597afb1fac";

    /// <summary>
    /// 空对象的 ETag
    /// </summary>
    /// <remarks>
    /// 读取时 SDK 同样核对：这是空内容的 MD5
    /// </remarks>
    private const string EmptyETag = "d41d8cd98f00b204e9800998ecf8427e";

    /// <summary>
    /// 用例的容器
    /// </summary>
    private static readonly ContainerName _container = new("media");

    /// <summary>
    /// 用例的键
    /// </summary>
    private static readonly BlobKey _key = new("a.txt");

    /// <summary>
    /// 内容类型的向量
    /// </summary>
    private static readonly ContentTypeVectors _vectors = SpecVectors.Read<ContentTypeVectors>("objectstorage", "blob-content-types.json");

    /// <summary>
    /// 两条路：命名实例（过出站地址策略的传输），缺省实例（SDK 自己的传输）
    /// </summary>
    public static TheoryData<bool> Transports => [true, false];

    /// <summary>
    /// 存储里的内容类型与 SDK 读出来交给 Provider 的值
    /// </summary>
    public static TheoryData<string, string> StoredAndRead => new()
    {
        // 解析不了的原样交出
        { "text/plain;", "text/plain;" },
        { "text/plain; charset=utf-8;", "text/plain; charset=utf-8;" },
        { "text/plain;;charset=utf-8", "text/plain;;charset=utf-8" },
        // .NET 的写法本身原样交出
        { "text/plain; charset=utf-8", "text/plain; charset=utf-8" },
        { "text/plain; boundary=\";;\"", "text/plain; boundary=\";;\"" },
        // 解析得了而写法不同的按 .NET 的写法交出：读取一侧做不到原样，是已知的缺口（RFC-0012「S3 Provider」）
        { "text/plain;charset=utf-8", "text/plain; charset=utf-8" },
        { "text/plain ; charset=utf-8", "text/plain; charset=utf-8" },
    };

    [Theory]
    [MemberData(nameof(Transports))]
    public async Task EveryValidContentType_IsWrittenAsGiven_OrRefusedWithoutARequest(bool named)
    {
        await using var endpoint = RecordingHttpListener.Answering("200 OK", $"ETag: \"{ContentETag}\"");
        await using var store = await CreateAsync(named, endpoint.Port);
        var refused = new List<string>();

        foreach (var contentType in _vectors.Valid)
        {
            var before = endpoint.Requests.Count;
            var put = await store.Store.PutAsync(_container, _key, new MemoryStream(_content), new BlobPutOptions { ContentType = contentType }, CancellationToken.None);
            var sent = endpoint.Requests.Skip(before).ToList();
            if (put.IsSuccess)
            {
                // 写进去的就是给的：线上的请求头逐字符相同，返回的元数据也是它
                var request = Assert.Single(sent);
                Assert.Equal(contentType, RecordingHttpListener.Header(request, "Content-Type"));
                Assert.Equal(contentType, put.ValueOrThrow().ContentType);
                continue;
            }

            // 写不出原值：一个请求都不发
            Assert.Equal(ObjectStorageErrors.ContentTypeUnsupported, put.Error?.Code);
            Assert.Equal(contentType, put.Error!.Details?["contentType"]);
            Assert.Empty(sent);
            refused.Add(contentType);
        }

        // 空参数与 ; 两侧的空格写法都写不出原值；.NET 的写法、大小写与引号照常写入
        Assert.Contains("text/plain;", refused);
        Assert.Contains("text/plain; charset=utf-8;", refused);
        Assert.Contains("text/plain;;charset=utf-8", refused);
        Assert.Contains("text/plain;charset=utf-8", refused);
        Assert.Contains("text/plain ; charset=utf-8", refused);
        Assert.DoesNotContain("text/plain", refused);
        Assert.DoesNotContain("text/plain; charset=utf-8", refused);
        Assert.DoesNotContain("Text/HTML", refused);
        Assert.DoesNotContain("text/plain; boundary=\";;\"", refused);
    }

    [Theory]
    [MemberData(nameof(Transports))]
    public async Task ARefusedContentType_SendsNothing_EvenForAWriteWithoutOverwriteOrAMultipartStart(bool named)
    {
        await using var endpoint = RecordingHttpListener.Answering("200 OK", $"ETag: \"{ContentETag}\"");
        await using var store = await CreateAsync(named, endpoint.Port);
        var options = new BlobPutOptions { ContentType = "text/plain; charset=utf-8;", Overwrite = false };

        // 不覆盖写入的 Head、分片上传的发起都不发：拒绝先于一切判定
        var put = await store.Store.PutAsync(_container, _key, new MemoryStream(_content), options, CancellationToken.None);
        var started = await ((IBlobMultipartStore)store.Store).StartAsync(_container, _key, options with { Overwrite = true }, CancellationToken.None);

        Assert.Equal(ObjectStorageErrors.ContentTypeUnsupported, put.Error?.Code);
        Assert.Equal(ObjectStorageErrors.ContentTypeUnsupported, started.Error?.Code);
        Assert.Equal(0, endpoint.ConnectionCount);
    }

    [Theory]
    [MemberData(nameof(StoredAndRead))]
    public async Task Reads_HandOverWhatTheSdkReads(string stored, string read)
    {
        await using var endpoint = RecordingHttpListener.Answering("200 OK", $"ETag: \"{EmptyETag}\"", "Last-Modified: Wed, 25 Nov 2026 00:00:00 GMT", "Content-Type: " + stored);
        await using var store = await CreateAsync(named: false, endpoint.Port);

        Assert.Equal(read, (await store.Store.HeadAsync(_container, _key, CancellationToken.None)).ValueOrThrow().ContentType);
        var got = (await store.Store.GetAsync(_container, _key, CancellationToken.None)).ValueOrThrow();
        await using (got)
        {
            Assert.Equal(read, got.Metadata.ContentType);
        }
    }

    [Fact]
    public async Task TheSdk_RewritesOrRejects_TheContentTypesTheStoreRefuses()
    {
        // 拒绝的理由：绕过 Provider 直接用 SDK 写，解析不了的抛出，写法不同的线上是改写后的值。SDK 哪天改成原样写出，这条失败，拒绝就该撤掉
        await using var endpoint = RecordingHttpListener.Answering("200 OK", $"ETag: \"{ContentETag}\"");
        var options = Options(endpoint.Port);
        using var client = S3ObjectStorageFeature.CreateClient(options);

        await Assert.ThrowsAsync<FormatException>(() => client.PutObjectAsync(Put("text/plain;"), CancellationToken.None));
        await client.PutObjectAsync(Put("text/plain;charset=utf-8"), CancellationToken.None);

        Assert.Equal("text/plain; charset=utf-8", RecordingHttpListener.Header(endpoint.Requests[^1], "Content-Type"));
        Assert.False(S3ContentTypeHeader.IsSentVerbatim("text/plain;"));
        Assert.False(S3ContentTypeHeader.IsSentVerbatim("text/plain;charset=utf-8"));
        Assert.True(S3ContentTypeHeader.IsSentVerbatim("text/plain; charset=utf-8"));
    }

    /// <summary>
    /// 直接交给 SDK 的写入请求
    /// </summary>
    private static PutObjectRequest Put(string contentType) => new()
    {
        BucketName = _container.Value,
        Key = _key.Value,
        InputStream = new MemoryStream(_content),
        ContentType = contentType,
    };

    /// <summary>
    /// 指向回环对端、不探测条件写入、只尝试一次的选项
    /// </summary>
    private static S3ObjectStorageOptions Options(int port) => new()
    {
        ServiceUrl = $"http://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}",
        AccessKey = "ak",
        SecretKey = "sk",
        ConditionalWrites = false,
        MaxAttempts = 1,
    };

    /// <summary>
    /// 建出指向回环对端的存储
    /// </summary>
    /// <remarks>
    /// <paramref name="named"/> 为真时经 <see cref="S3BlobStoreProvider"/> 按定义建出，客户端过出站地址策略（放行回环）；为假时照缺省实例的做法，用 SDK 自己的传输
    /// </remarks>
    private static async Task<OwnedStore> CreateAsync(bool named, int port)
    {
        var logs = new LogCapture();
        if (named)
        {
            var provider = new S3BlobStoreProvider(TimeProvider.System, logs, new OutboundAddressPolicy(new OutboundAddressOptions { AllowLoopback = true }, logs), new ConfigurationBuilder().Build());
            var settings = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ServiceUrl"] = $"http://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}",
                ["AccessKey"] = "ak",
                ["SecretKey"] = "sk",
                ["ConditionalWrites"] = "false",
                ["MaxAttempts"] = "1",
            };
            var created = (await provider.CreateAsync(new BlobStoreDefinition("tenant-a", S3BlobStoreProvider.ProviderName, settings), CancellationToken.None)).ValueOrThrow();
            return new OwnedStore(created, (IAsyncDisposable)created);
        }

        var options = Options(port);
        var client = S3ObjectStorageFeature.CreateClient(options);
        return new OwnedStore(new S3BlobStore(client, options, TimeProvider.System, logs), new ClientLease(client));
    }

    /// <summary>
    /// 存储连同它用完要释放的东西
    /// </summary>
    /// <param name="Store">存储</param>
    /// <param name="Owner">用完释放的对象：命名实例自己，或缺省实例的客户端</param>
    private sealed record OwnedStore(IBlobStore Store, IAsyncDisposable Owner) : IAsyncDisposable
    {
        /// <inheritdoc />
        public ValueTask DisposeAsync() => Owner.DisposeAsync();
    }

    /// <summary>
    /// 把客户端的释放包成异步释放
    /// </summary>
    /// <param name="client">客户端</param>
    private sealed class ClientLease(IDisposable client) : IAsyncDisposable
    {
        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            client.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="MaxLength">长度上限</param>
    /// <param name="Default">写入时不给内容类型的缺省值</param>
    /// <param name="Valid">合法的内容类型</param>
    /// <param name="Invalid">不合法的内容类型</param>
    private sealed record ContentTypeVectors(int MaxLength, string Default, string[] Valid, string[] Invalid);
}
