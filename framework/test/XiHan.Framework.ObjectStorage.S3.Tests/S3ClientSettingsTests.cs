// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Abstractions.Signing;
using XiHan.Framework.ObjectStorage.Abstractions.Stores;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 会话令牌、请求超时、尝试次数与代理从设置落到客户端的配置上（ADR-0281）
/// </summary>
/// <remarks>
/// 不连任何服务：核对的是「选项 → 客户端配置与凭据」这一步。配置节与命名实例的设置是同一套键，两个来源各走一遍；不写这些键时配置与没有这些选项时相同；不合法的值与只给了一部分的成组设置在读取时失败，凭据不出现在打印、异常消息、返回的错误与日志里
/// </remarks>
public sealed class S3ClientSettingsTests
{
    /// <summary>
    /// 用例里的会话令牌
    /// </summary>
    private const string SessionToken = "FwoGZXIvYXdzEXAMPLE-session-token";

    /// <summary>
    /// 用例里的代理用户名
    /// </summary>
    private const string ProxyUserName = "proxy-operator";

    /// <summary>
    /// 用例里的代理口令
    /// </summary>
    private const string ProxyPassword = "pr0xy-Passw0rd-EXAMPLE";

    /// <summary>
    /// 两个来源：配置节，命名实例的设置
    /// </summary>
    public static TheoryData<bool> Sources => [true, false];

    /// <summary>
    /// 不合法的单个值：键、值
    /// </summary>
    public static TheoryData<string, string> InvalidValues => new()
    {
        { "RequestTimeoutSeconds", "0" },
        { "RequestTimeoutSeconds", "-1" },
        { "RequestTimeoutSeconds", "86400.5" },
        { "RequestTimeoutSeconds", "1e-9" },
        { "RequestTimeoutSeconds", "NaN" },
        { "RequestTimeoutSeconds", "Infinity" },
        { "RequestTimeoutSeconds", "soon" },
        { "RequestTimeoutSeconds", "" },
        { "MaxAttempts", "0" },
        { "MaxAttempts", "-1" },
        { "MaxAttempts", "11" },
        { "MaxAttempts", "2.5" },
        { "MaxAttempts", "many" },
        { "MaxAttempts", "" },
        { "ProxyPort", "0" },
        { "ProxyPort", "65536" },
        { "ProxyPort", "http" },
        { "ProxyHost", "http://proxy.internal" },
        { "ProxyHost", "proxy.internal:3128" },
        { "ProxyHost", "proxy.internal/path" },
        { "ProxyHost", "proxy internal" },
        { "ProxyHost", "fe80::1%eth0" },
    };

    /// <summary>
    /// 只给了一部分的成组设置：给出的键，以逗号分隔
    /// </summary>
    public static TheoryData<string> PartialGroups =>
    [
        "SessionToken",
        "SessionToken,AccessKey",
        "SessionToken,SecretKey",
        "ProxyHost",
        "ProxyPort",
        "ProxyUserName",
        "ProxyPassword",
        "ProxyUserName,ProxyPassword",
        "ProxyHost,ProxyPort,ProxyUserName",
        "ProxyHost,ProxyPort,ProxyPassword",
        "ProxyHost,ProxyUserName,ProxyPassword",
    ];

    [Theory]
    [MemberData(nameof(Sources))]
    public void WithoutTheSettings_TheClientIsWhatItWasBefore(bool fromConfiguration)
    {
        var options = Read(fromConfiguration, ("ServiceUrl", "http://localhost:9000"), ("AccessKey", "ak"), ("SecretKey", "sk"));

        Assert.Null(options.SessionToken);
        Assert.Null(options.RequestTimeout);
        Assert.Null(options.MaxAttempts);
        Assert.Null(options.ProxyHost);
        Assert.Null(options.ProxyPort);
        Assert.Null(options.ProxyUserName);
        Assert.Null(options.ProxyPassword);

        // 与一份什么都没设过的 SDK 配置逐项相同：没给的设置一个字段都不碰
        var untouched = new AmazonS3Config();
        var config = S3ObjectStorageFeature.CreateClientConfig(options, null);
        Assert.Equal(untouched.Timeout, config.Timeout);
        Assert.Equal(untouched.MaxErrorRetry, config.MaxErrorRetry);
        Assert.False(config.IsMaxErrorRetrySet);
        Assert.Equal(untouched.RetryMode, config.RetryMode);
        Assert.Equal(untouched.ProxyHost, config.ProxyHost);
        Assert.Equal(untouched.ProxyPort, config.ProxyPort);
        Assert.Null(config.ProxyCredentials);
        Assert.Null(config.GetWebProxy());

        // 两项密钥是静态凭据，不带令牌；都没有时交给 SDK 的凭据链
        var credentials = Assert.IsType<BasicAWSCredentials>(S3ObjectStorageFeature.CreateCredentials(options)).GetCredentials();
        Assert.Equal(("ak", "sk", false), (credentials.AccessKey, credentials.SecretKey, credentials.UseToken));
        Assert.Null(S3ObjectStorageFeature.CreateCredentials(Read(fromConfiguration, ("ServiceUrl", "http://localhost:9000"))));
    }

    [Theory]
    [MemberData(nameof(Sources))]
    public void TheSettings_ReachTheClientConfiguration(bool fromConfiguration)
    {
        var options = Read(fromConfiguration, FullSettings());

        Assert.Equal(SessionToken, options.SessionToken);
        Assert.Equal(TimeSpan.FromSeconds(2.5), options.RequestTimeout);
        Assert.Equal(4, options.MaxAttempts);
        Assert.Equal(("proxy.internal", 3128), (options.ProxyHost, options.ProxyPort));
        Assert.Equal((ProxyUserName, ProxyPassword), (options.ProxyUserName, options.ProxyPassword));

        using var client = S3ObjectStorageFeature.CreateClient(options);
        var config = Assert.IsType<AmazonS3Config>(client.Config);
        Assert.Equal(TimeSpan.FromSeconds(2.5), config.Timeout);
        // SDK 记的是首次之外的重试次数
        Assert.True(config.IsMaxErrorRetrySet);
        Assert.Equal(3, config.MaxErrorRetry);
        Assert.Equal(("proxy.internal", 3128), (config.ProxyHost, config.ProxyPort));
        Assert.Equal(new Uri("http://proxy.internal:3128/"), config.GetWebProxy().GetProxy(new Uri("https://s3.example.com/bucket/key")));
        var proxyCredentials = Assert.IsType<NetworkCredential>(config.ProxyCredentials);
        Assert.Equal((ProxyUserName, ProxyPassword), (proxyCredentials.UserName, proxyCredentials.Password));

        var credentials = Assert.IsType<SessionAWSCredentials>(S3ObjectStorageFeature.CreateCredentials(options)).GetCredentials();
        Assert.Equal(("ak", "sk", SessionToken, true), (credentials.AccessKey, credentials.SecretKey, credentials.Token, credentials.UseToken));
    }

    [Fact]
    public void TheLimits_AreAcceptedAtBothEnds()
    {
        Assert.Equal(TimeSpan.FromHours(24), S3ObjectStorageOptions.MaxRequestTimeout);
        Assert.Equal(10, S3ObjectStorageOptions.MaxAttemptsLimit);

        var widest = Read(fromConfiguration: true, ("RequestTimeoutSeconds", "86400"), ("MaxAttempts", "10"), ("ProxyHost", "10.0.0.8"), ("ProxyPort", "65535"));
        Assert.Equal((S3ObjectStorageOptions.MaxRequestTimeout, 10, 65535), (widest.RequestTimeout, widest.MaxAttempts, widest.ProxyPort));
        var narrowest = Read(fromConfiguration: false, ("RequestTimeoutSeconds", "0.001"), ("MaxAttempts", "1"), ("ProxyHost", "proxy"), ("ProxyPort", "1"));
        Assert.Equal((TimeSpan.FromMilliseconds(1), 1, 1), (narrowest.RequestTimeout, narrowest.MaxAttempts, narrowest.ProxyPort));

        // 一次尝试即不重试
        Assert.Equal(0, S3ObjectStorageFeature.CreateClientConfig(narrowest, null).MaxErrorRetry);

        // 直接构造选项时同样核对范围
        Assert.Throws<ArgumentOutOfRangeException>(static () => new S3ObjectStorageOptions { RequestTimeout = TimeSpan.Zero });
        Assert.Throws<ArgumentOutOfRangeException>(static () => new S3ObjectStorageOptions { RequestTimeout = S3ObjectStorageOptions.MaxRequestTimeout + TimeSpan.FromTicks(1) });
        Assert.Throws<ArgumentOutOfRangeException>(static () => new S3ObjectStorageOptions { MaxAttempts = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(static () => new S3ObjectStorageOptions { MaxAttempts = S3ObjectStorageOptions.MaxAttemptsLimit + 1 });
        Assert.Throws<ArgumentOutOfRangeException>(static () => new S3ObjectStorageOptions { ProxyPort = 0 });
        Assert.Throws<ArgumentException>(static () => new S3ObjectStorageOptions { ProxyHost = "http://proxy.internal" });
    }

    [Fact]
    public void AnIPv6ProxyHost_IsBracketedForTheSdk()
    {
        foreach (var host in new[] { "::1", "[::1]" })
        {
            var config = S3ObjectStorageFeature.CreateClientConfig(new S3ObjectStorageOptions { ProxyHost = host, ProxyPort = 3128 }, null);

            Assert.Equal(new Uri("http://[::1]:3128/"), config.GetWebProxy().GetProxy(new Uri("http://s3.example.com/")));
        }
    }

    [Theory]
    [MemberData(nameof(InvalidValues))]
    public void AnInvalidValue_FailsNamingTheKey(string key, string value)
    {
        // 代理的两项成对给出，免得先撞上「只给了一部分」
        var others = key switch
        {
            "ProxyHost" => new[] { ("ProxyPort", "3128") },
            "ProxyPort" => [("ProxyHost", "proxy.internal")],
            _ => [],
        };

        var configured = Assert.ThrowsAny<ArgumentException>(() => Read(fromConfiguration: true, [(key, value), .. others]));
        Assert.Contains($"'{S3ObjectStorageOptions.ConfigurationSection}:{key}'", configured.Message, StringComparison.Ordinal);

        var named = Assert.ThrowsAny<ArgumentException>(() => Read(fromConfiguration: false, [(key, value), .. others]));
        Assert.Contains($"'{key}'", named.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(PartialGroups))]
    public void APartialGroup_Fails_WithoutEchoingAnyValue(string given)
    {
        var all = FullSettings().ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
        var settings = given.Split(',').Select(key => (key, all[key])).ToArray();

        foreach (var fromConfiguration in new[] { true, false })
        {
            var exception = Assert.ThrowsAny<ArgumentException>(() => Read(fromConfiguration, settings));

            Assert.DoesNotContain(SessionToken, exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(ProxyUserName, exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(ProxyPassword, exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("proxy.internal", exception.Message, StringComparison.Ordinal);
        }

        // 手工构造的选项在建客户端时同样被拒，不会悄悄退回凭据链或不走代理
        var built = new S3ObjectStorageOptions
        {
            AccessKey = settings.Any(static s => s.key == "AccessKey") ? "ak" : null,
            SecretKey = settings.Any(static s => s.key == "SecretKey") ? "sk" : null,
            SessionToken = settings.Any(static s => s.key == "SessionToken") ? SessionToken : null,
            ProxyHost = settings.Any(static s => s.key == "ProxyHost") ? "proxy.internal" : null,
            ProxyPort = settings.Any(static s => s.key == "ProxyPort") ? 3128 : null,
            ProxyUserName = settings.Any(static s => s.key == "ProxyUserName") ? ProxyUserName : null,
            ProxyPassword = settings.Any(static s => s.key == "ProxyPassword") ? ProxyPassword : null,
        };
        Assert.Throws<ArgumentException>(() => S3ObjectStorageFeature.CreateClientConfig(built, null));
        Assert.Throws<ArgumentException>(() => S3ObjectStorageFeature.CreateCredentials(built));
    }

    [Fact]
    public void AProxyHostCarryingCredentials_IsRejected_WithoutEchoingThem()
    {
        var host = $"{ProxyUserName}:{ProxyPassword}@proxy.internal";

        foreach (var fromConfiguration in new[] { true, false })
        {
            var exception = Assert.ThrowsAny<ArgumentException>(() => Read(fromConfiguration, ("ProxyHost", host), ("ProxyPort", "3128")));

            Assert.Contains("ProxyHost", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(ProxyPassword, exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(ProxyUserName, exception.Message, StringComparison.Ordinal);
        }

        var direct = Assert.Throws<ArgumentException>(() => new S3ObjectStorageOptions { ProxyHost = host });
        Assert.DoesNotContain(ProxyPassword, direct.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheBinding_SuppliesTheSessionToken_AndTheGroupIsCheckedAfterItOverrides()
    {
        static BindingValue Binding(params (string Key, string Value)[] properties) => new()
        {
            Name = S3ObjectStorageFeature.ConnectionBinding,
            Type = "blob",
            Value = null,
            Properties = properties.ToDictionary(static p => p.Key, static p => p.Value, StringComparer.Ordinal),
        };

        // 令牌来自绑定、密钥来自配置节：覆盖之后是完整的一组
        var mixed = S3ObjectStorageOptions.FromConfiguration(Configuration(("AccessKey", "ak"), ("SecretKey", "sk"), ("SessionToken", "stale")), Maybe.Some(Binding(("sessionToken", SessionToken))));
        Assert.Equal(("ak", "sk", SessionToken), (mixed.AccessKey, mixed.SecretKey, mixed.SessionToken));

        // 空白的属性不算给出，照读配置节
        var blank = S3ObjectStorageOptions.FromConfiguration(Configuration(("AccessKey", "ak"), ("SecretKey", "sk"), ("SessionToken", SessionToken)), Maybe.Some(Binding(("sessionToken", " "))));
        Assert.Equal(SessionToken, blank.SessionToken);

        // 绑定只给了令牌、哪里都没有密钥：读取失败，令牌不进消息
        var exception = Assert.Throws<ArgumentException>(() => S3ObjectStorageOptions.FromConfiguration(Configuration(), Maybe.Some(Binding(("sessionToken", SessionToken)))));
        Assert.DoesNotContain(SessionToken, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_MasksTheSessionTokenAndTheProxyCredentials()
    {
        var text = Read(fromConfiguration: true, FullSettings()).ToString();

        Assert.Contains($"SessionToken = {Redaction.Mask},", text, StringComparison.Ordinal);
        Assert.Contains($"ProxyUserName = {Redaction.Mask}, ProxyPassword = {Redaction.Mask}", text, StringComparison.Ordinal);
        Assert.DoesNotContain(SessionToken, text, StringComparison.Ordinal);
        Assert.DoesNotContain(ProxyUserName, text, StringComparison.Ordinal);
        Assert.DoesNotContain(ProxyPassword, text, StringComparison.Ordinal);

        // 不是凭据的照常打印：排查连接问题要用到
        Assert.Contains("RequestTimeout = 00:00:02.5000000, MaxAttempts = 4, ProxyHost = proxy.internal, ProxyPort = 3128,", text, StringComparison.Ordinal);
        // 没配时打印为空，看得出没有配置
        Assert.Contains("SessionToken = ,", new S3ObjectStorageOptions().ToString(), StringComparison.Ordinal);
        Assert.EndsWith("RequestTimeout = , MaxAttempts = , ProxyHost = , ProxyPort = , ProxyUserName = , ProxyPassword =  }", new S3ObjectStorageOptions().ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASignedUrl_CarriesTheSessionToken_OnlyWhenThereIsOne()
    {
        // 临时凭据签出的地址必须带着令牌，否则服务不认这个签名。签名是本地计算，不连服务
        var container = new ContainerName("docs");
        var key = new BlobKey("a/report.txt");
        var temporary = new S3ObjectStorageOptions { ServiceUrl = "http://s3.invalid:9000", AccessKey = "ak", SecretKey = "sk", SessionToken = SessionToken };
        using var temporaryClient = S3ObjectStorageFeature.CreateClient(temporary);
        var withToken = (await new S3BlobStore(temporaryClient, temporary, TimeProvider.System, new LogCapture()).SignAsync(container, key, BlobUrlRequest.ForDownload(), CancellationToken.None)).ValueOrThrow();

        Assert.Contains("X-Amz-Security-Token=" + Uri.EscapeDataString(SessionToken), withToken.Url.Query, StringComparison.Ordinal);
        Assert.Contains("X-Amz-Signature=", withToken.Url.Query, StringComparison.Ordinal);

        var permanent = temporary with { SessionToken = null };
        using var permanentClient = S3ObjectStorageFeature.CreateClient(permanent);
        var withoutToken = (await new S3BlobStore(permanentClient, permanent, TimeProvider.System, new LogCapture()).SignAsync(container, key, BlobUrlRequest.ForDownload(), CancellationToken.None)).ValueOrThrow();

        Assert.DoesNotContain("X-Amz-Security-Token", withoutToken.Url.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ANamedInstance_BuildsItsClientFromItsOwnSettings()
    {
        S3ObjectStorageOptions? seen = null;
        // 设置里配了代理：运营方打开了命名实例的代理开关（ADR-0294）
        var provider = new S3BlobStoreProvider(TimeProvider.System, new LogCapture(), options =>
        {
            seen = options;
            return FakeS3Client.Create().AsClient();
        }, ambientCredentials: false, allowProxy: true);
        var settings = FullSettings().Append(("ConditionalWrites", "false")).ToDictionary(static pair => pair.Item1, static pair => pair.Item2, StringComparer.Ordinal);

        var created = await provider.CreateAsync(new BlobStoreDefinition("tenant-a", S3BlobStoreProvider.ProviderName, settings), CancellationToken.None);

        Assert.True(created.IsSuccess, created.ToString());
        Assert.NotNull(seen);
        Assert.Equal((SessionToken, TimeSpan.FromSeconds(2.5), 4), (seen.SessionToken, seen.RequestTimeout, seen.MaxAttempts));
        Assert.Equal(("proxy.internal", 3128, ProxyUserName, ProxyPassword), (seen.ProxyHost, seen.ProxyPort, seen.ProxyUserName, seen.ProxyPassword));
        await ((IAsyncDisposable)created.ValueOrThrow()).DisposeAsync();
    }

    [Fact]
    public async Task ANamedInstanceWithAPartialGroup_IsAnInvalidDefinition_AndLeaksNoCredential()
    {
        var logs = new LogCapture();
        var provider = new S3BlobStoreProvider(TimeProvider.System, logs, static _ => throw new InvalidOperationException("No client may be built for an invalid definition."), ambientCredentials: false, allowProxy: true);
        var settings = new Dictionary<string, string>
        {
            ["ServiceUrl"] = "http://tenant-store:9000",
            ["AccessKey"] = "ak",
            ["SessionToken"] = SessionToken,
            ["ProxyHost"] = "proxy.internal",
            ["ProxyUserName"] = ProxyUserName,
            ["ProxyPassword"] = ProxyPassword,
            ["ConditionalWrites"] = "false",
        };

        var created = await provider.CreateAsync(new BlobStoreDefinition("tenant-a", S3BlobStoreProvider.ProviderName, settings), CancellationToken.None);

        Assert.Equal(ObjectStorageErrors.StoreDefinitionInvalid, created.Error?.Code);
        var reported = created.Error!.ToString() + created.Error.Details?["reason"];
        string[] secrets = [SessionToken, ProxyUserName, ProxyPassword];
        Assert.All(secrets, secret => Assert.DoesNotContain(secret, reported, StringComparison.Ordinal));
        Assert.All(logs.Entries, entry => Assert.All(secrets, secret => Assert.DoesNotContain(secret, entry.Message + entry.Exception, StringComparison.Ordinal)));
    }

    /// <summary>
    /// 四样都给全的一组设置
    /// </summary>
    private static (string Key, string Value)[] FullSettings() =>
    [
        ("ServiceUrl", "http://localhost:9000"),
        ("AccessKey", "ak"),
        ("SecretKey", "sk"),
        ("SessionToken", SessionToken),
        ("RequestTimeoutSeconds", "2.5"),
        ("MaxAttempts", "4"),
        ("ProxyHost", "proxy.internal"),
        ("ProxyPort", "3128"),
        ("ProxyUserName", ProxyUserName),
        ("ProxyPassword", ProxyPassword),
    ];

    /// <summary>
    /// 从配置节或命名实例的设置读出选项
    /// </summary>
    /// <remarks>
    /// 键是相对配置节的名字，两个来源用同一套
    /// </remarks>
    private static S3ObjectStorageOptions Read(bool fromConfiguration, params (string Key, string Value)[] settings)
        => fromConfiguration
            ? S3ObjectStorageOptions.FromConfiguration(Configuration(settings))
            : S3ObjectStorageOptions.FromSettings(settings.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal));

    /// <summary>
    /// 以配置节下的键值构造内存配置
    /// </summary>
    private static IConfiguration Configuration(params (string Key, string Value)[] settings)
        => new ConfigurationBuilder().AddInMemoryCollection(settings.Select(static pair => new KeyValuePair<string, string?>($"{S3ObjectStorageOptions.ConfigurationSection}:{pair.Key}", pair.Value))).Build();
}
