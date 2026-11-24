// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Abstractions.Stores;
using XiHan.Framework.Testing;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 会话令牌、请求超时、尝试次数与代理在线上生效（ADR-0281）
/// </summary>
/// <remarks>
/// 走真实的 SDK 与套接字，对端是本机回环上的 <see cref="RecordingHttpListener"/>，不需要 S3 服务。缺省实例的客户端用 SDK 自己的传输，命名实例的客户端用过出站地址策略的传输（<see cref="S3OutboundHttpClientFactory"/>），两条路各走一遍；命名实例另核对代理的地址过策略
/// </remarks>
public sealed class S3ClientSettingsWireTests
{
    /// <summary>
    /// 用例里的会话令牌
    /// </summary>
    private const string SessionToken = "FwoGZXIvYXdzEXAMPLE-wire-session-token";

    /// <summary>
    /// 用例里的代理用户名
    /// </summary>
    private const string ProxyUserName = "wire-proxy-operator";

    /// <summary>
    /// 用例里的代理口令
    /// </summary>
    private const string ProxyPassword = "w1re-pr0xy-Passw0rd-EXAMPLE";

    /// <summary>
    /// 经代理访问的端点：只出现在请求行里，没有任何一方去解析它
    /// </summary>
    private const string ProxiedServiceUrl = "http://s3.example.test:9000";

    /// <summary>
    /// 用例的容器
    /// </summary>
    private static readonly ContainerName _container = new("media");

    /// <summary>
    /// 用例的键
    /// </summary>
    private static readonly BlobKey _key = new("a.txt");

    /// <summary>
    /// 两条路：命名实例（过出站地址策略的传输），缺省实例（SDK 自己的传输）
    /// </summary>
    public static TheoryData<bool> Transports => [true, false];

    [Theory]
    [MemberData(nameof(Transports))]
    public async Task TheProxy_CarriesEveryAttempt_WithItsCredentialsAndTheSessionToken(bool named)
    {
        await using var proxy = RecordingHttpListener.ProxyRequiring(ProxyUserName, ProxyPassword, "502 Bad Gateway");
        var logs = new LogCapture();
        await using var store = await CreateAsync(named, logs, allowLoopback: true,
            ("ServiceUrl", ProxiedServiceUrl),
            ("SessionToken", SessionToken),
            ("MaxAttempts", "2"),
            ("ProxyHost", "127.0.0.1"),
            ("ProxyPort", proxy.Port.ToString(CultureInfo.InvariantCulture)),
            ("ProxyUserName", ProxyUserName),
            ("ProxyPassword", ProxyPassword));

        var head = await store.Store.HeadAsync(_container, _key, CancellationToken.None);

        Assert.Equal(ObjectStorageErrors.StoreUnavailable, head.Error?.Code);
        var requests = proxy.Requests;
        // 每个请求都是给代理的绝对地址形式，带着临时凭据的令牌头
        Assert.All(requests, static request =>
        {
            Assert.StartsWith($"HEAD {ProxiedServiceUrl}/media/a.txt HTTP/1.1", request, StringComparison.Ordinal);
            Assert.Equal(SessionToken, RecordingHttpListener.Header(request, "X-Amz-Security-Token"));
        });
        // 带对了代理凭据的请求恰好是尝试次数那么多：代理回 502，SDK 重试到上限为止；没带凭据的只是被 407 打回的那几次
        var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(ProxyUserName + ":" + ProxyPassword));
        Assert.Equal(2, requests.Count(request => RecordingHttpListener.Header(request, "Proxy-Authorization") == expected));
        Assert.Contains(requests, static request => RecordingHttpListener.Header(request, "Proxy-Authorization") is null);
        AssertNoCredentialIn(logs, head.Error!.ToString());
    }

    [Theory]
    [MemberData(nameof(Transports))]
    public async Task AProxyWithoutCredentials_CarriesTheRequestToo(bool named)
    {
        await using var proxy = RecordingHttpListener.Answering("502 Bad Gateway");
        await using var store = await CreateAsync(named, new LogCapture(), allowLoopback: true,
            ("ServiceUrl", ProxiedServiceUrl),
            ("MaxAttempts", "1"),
            ("ProxyHost", "127.0.0.1"),
            ("ProxyPort", proxy.Port.ToString(CultureInfo.InvariantCulture)));

        var head = await store.Store.HeadAsync(_container, _key, CancellationToken.None);

        Assert.Equal(ObjectStorageErrors.StoreUnavailable, head.Error?.Code);
        var request = Assert.Single(proxy.Requests);
        Assert.StartsWith($"HEAD {ProxiedServiceUrl}/media/a.txt HTTP/1.1", request, StringComparison.Ordinal);
        Assert.Null(RecordingHttpListener.Header(request, "Proxy-Authorization"));
        // 没配会话令牌就没有令牌头
        Assert.Null(RecordingHttpListener.Header(request, "X-Amz-Security-Token"));
    }

    [Fact]
    public async Task TheProxyOfANamedInstance_PassesTheOutboundAddressPolicy()
    {
        await using var proxy = RecordingHttpListener.Answering("502 Bad Gateway");
        var logs = new LogCapture();
        // 缺省的策略不放行回环：代理的地址与端点一样可能是租户填的
        await using var store = await CreateAsync(named: true, logs, allowLoopback: false,
            ("ServiceUrl", ProxiedServiceUrl),
            ("MaxAttempts", "1"),
            ("ProxyHost", "127.0.0.1"),
            ("ProxyPort", proxy.Port.ToString(CultureInfo.InvariantCulture)),
            ("ProxyUserName", ProxyUserName),
            ("ProxyPassword", ProxyPassword));

        var head = await store.Store.HeadAsync(_container, _key, CancellationToken.None);

        Assert.Equal(ObjectStorageErrors.StoreUnavailable, head.Error?.Code);
        Assert.Equal(0, proxy.ConnectionCount);
        Assert.Empty(proxy.Requests);
        Assert.Contains(logs.Entries, static entry => entry.Category == typeof(OutboundAddressPolicy).FullName && entry.Level == LogLevel.Warning);
        AssertNoCredentialIn(logs, head.Error!.ToString());
    }

    [Theory]
    [MemberData(nameof(Transports))]
    public async Task MaxAttempts_BoundsTheRequestsOfOneOperation(bool named)
    {
        await using var endpoint = RecordingHttpListener.Answering("500 Internal Server Error");
        await using var store = await CreateAsync(named, new LogCapture(), allowLoopback: true,
            ("ServiceUrl", $"http://127.0.0.1:{endpoint.Port.ToString(CultureInfo.InvariantCulture)}"),
            ("MaxAttempts", "1"));

        var head = await store.Store.HeadAsync(_container, _key, CancellationToken.None);

        // 一次尝试即不重试：服务端错误本来会重试
        Assert.Equal(ObjectStorageErrors.StoreUnavailable, head.Error?.Code);
        Assert.StartsWith("HEAD /media/a.txt HTTP/1.1", Assert.Single(endpoint.Requests), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Transports))]
    public async Task RequestTimeout_EndsARequestThatIsNeverAnswered(bool named)
    {
        await using var endpoint = RecordingHttpListener.Silent();
        await using var store = await CreateAsync(named, new LogCapture(), allowLoopback: true,
            ("ServiceUrl", $"http://127.0.0.1:{endpoint.Port.ToString(CultureInfo.InvariantCulture)}"),
            ("RequestTimeoutSeconds", "2"),
            ("MaxAttempts", "1"));
        var watch = Stopwatch.StartNew();

        var head = await store.Store.HeadAsync(_container, _key, CancellationToken.None);

        // 不设超时这个请求不会自己结束（SDK 对 S3 的缺省约 24.8 天）；上界留得很宽，只防挂死
        Assert.Equal(ObjectStorageErrors.StoreUnavailable, head.Error?.Code);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(30), $"The request ended after {watch.Elapsed}.");
        Assert.Single(endpoint.Requests);
    }

    [Fact]
    public async Task AProbeThatTimesOut_CountsAsUnreachable_AndTheInstanceIsStillCreated()
    {
        await using var endpoint = RecordingHttpListener.Silent();
        var logs = new LogCapture();
        var provider = new S3BlobStoreProvider(TimeProvider.System, logs, new OutboundAddressPolicy(new OutboundAddressOptions { AllowLoopback = true }, logs));
        var settings = new Dictionary<string, string>
        {
            ["ServiceUrl"] = $"http://127.0.0.1:{endpoint.Port.ToString(CultureInfo.InvariantCulture)}",
            ["AccessKey"] = "ak",
            ["SecretKey"] = "sk",
            ["HealthBucket"] = "probe",
            ["RequestTimeoutSeconds"] = "1",
            ["MaxAttempts"] = "1",
        };

        // 条件写入的探测撞上请求超时：与连不上同样是尚未确认，实例照常建出、后台重试，不当成探测自己出了意外
        var created = (await provider.CreateAsync(new BlobStoreDefinition("tenant-a", S3BlobStoreProvider.ProviderName, settings), CancellationToken.None)).ValueOrThrow();
        await using ((IAsyncDisposable)created)
        {
            Assert.NotEmpty(endpoint.Requests);
            Assert.Contains(logs.Entries, static entry => entry.Level == LogLevel.Warning && entry.Exception is TimeoutException);
            Assert.DoesNotContain(logs.Entries, static entry => entry.Level >= LogLevel.Error);
        }
    }

    /// <summary>
    /// 按设置建出一个不探测条件写入的存储
    /// </summary>
    /// <remarks>
    /// <paramref name="named"/> 为真时经 <see cref="S3BlobStoreProvider"/> 按定义建出，客户端过出站地址策略；为假时照缺省实例的做法，从配置节读选项、用 SDK 自己的传输。两者的设置键相同
    /// </remarks>
    private static async Task<OwnedStore> CreateAsync(bool named, LogCapture logs, bool allowLoopback, params (string Key, string Value)[] settings)
    {
        (string Key, string Value)[] all = [.. settings, ("AccessKey", "ak"), ("SecretKey", "sk"), ("ConditionalWrites", "false")];
        if (named)
        {
            var provider = new S3BlobStoreProvider(TimeProvider.System, logs, new OutboundAddressPolicy(new OutboundAddressOptions { AllowLoopback = allowLoopback }, logs));
            var definition = new BlobStoreDefinition("tenant-a", S3BlobStoreProvider.ProviderName, all.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal));
            var created = (await provider.CreateAsync(definition, CancellationToken.None)).ValueOrThrow();
            return new OwnedStore(created, (IAsyncDisposable)created);
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(all.Select(static pair => new KeyValuePair<string, string?>($"{S3ObjectStorageOptions.ConfigurationSection}:{pair.Key}", pair.Value)))
            .Build();
        var options = S3ObjectStorageOptions.FromConfiguration(configuration);
        var client = S3ObjectStorageFeature.CreateClient(options);
        return new OwnedStore(new S3BlobStore(client, options, TimeProvider.System, logs), new ClientLease(client));
    }

    /// <summary>
    /// 返回的错误与全部日志（消息与异常全文）里都没有凭据
    /// </summary>
    private static void AssertNoCredentialIn(LogCapture logs, string reported)
    {
        string[] secrets = [SessionToken, ProxyUserName, ProxyPassword, Convert.ToBase64String(Encoding.UTF8.GetBytes(ProxyUserName + ":" + ProxyPassword))];
        var entries = logs.Entries;
        // 失败的操作必定记了日志：没有日志时下面的断言什么都没核对
        Assert.NotEmpty(entries);
        Assert.All(secrets, secret =>
        {
            Assert.DoesNotContain(secret, reported, StringComparison.Ordinal);
            Assert.All(entries, entry => Assert.DoesNotContain(secret, entry.Message + entry.Exception, StringComparison.Ordinal));
        });
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
}
