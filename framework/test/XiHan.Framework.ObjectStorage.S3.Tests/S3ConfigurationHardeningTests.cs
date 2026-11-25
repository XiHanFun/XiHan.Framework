// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Stores;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// S3 配置的加固（ADR-0294、ADR-0302）
/// </summary>
/// <remarks>
/// 命名实例配代理要运营方打开开关；服务端点不合法时报错不带原值，带用户信息的端点在配置节、绑定的主值、命名实例的设置与直接构造里都被拒；缺省实例只给一项密钥与命名实例一样是配置错误。都不连任何服务
/// </remarks>
public sealed class S3ConfigurationHardeningTests
{
    /// <summary>
    /// 用例里的代理主机
    /// </summary>
    private const string ProxyHost = "egress-proxy.corp.internal";

    /// <summary>
    /// 用例里的代理用户名
    /// </summary>
    private const string ProxyUserName = "hardening-proxy-user";

    /// <summary>
    /// 用例里的代理口令
    /// </summary>
    private const string ProxyPassword = "h4rdening-Pr0xy-EXAMPLE";

    /// <summary>
    /// 写进端点里的口令
    /// </summary>
    private const string EndpointPassword = "endp0int-Passw0rd-EXAMPLE";

    /// <summary>
    /// 写进端点里的用户名
    /// </summary>
    private const string EndpointUser = "endp0int-user-EXAMPLE";

    /// <summary>
    /// 不合法的服务端点，各自带着口令
    /// </summary>
    public static TheoryData<string> InvalidServiceUrls =>
    [
        $"ftp://admin:{EndpointPassword}@s3.internal:9000",
        $"admin:{EndpointPassword}@s3.internal:9000",
        $"//admin:{EndpointPassword}@s3.internal:9000",
    ];

    /// <summary>
    /// 带用户信息的合法地址
    /// </summary>
    /// <remarks>
    /// 用户名都是 <see cref="EndpointUser"/>；空的 <c>@</c> 与只有冒号的也算用户信息（ADR-0302）
    /// </remarks>
    public static TheoryData<string> ServiceUrlsWithUserInformation =>
    [
        $"http://{EndpointUser}:{EndpointPassword}@s3.internal:9000",
        $"https://{EndpointUser}@s3.internal",
        $"HTTPS://{EndpointUser}:{EndpointPassword}@S3.INTERNAL:9000/bucket",
        $"http://{EndpointUser}:@s3.internal:9000",
        "http://:@s3.internal:9000",
        "http://@s3.internal:9000",
        $"http:\\\\{EndpointUser}:{EndpointPassword}@s3.internal:9000",
    ];

    /// <summary>
    /// 只有一项的密钥：键、值
    /// </summary>
    public static TheoryData<string, string> OneSidedKeys => new()
    {
        { "AccessKey", "AKIAONESIDEDEXAMPLE" },
        { "SecretKey", "one-sided-secret-EXAMPLE" },
    };

    [Fact]
    public async Task ANamedInstanceWithAProxy_IsRejected_UnlessTheOperatorAllowsIt()
    {
        var logs = new LogCapture();
        var denied = new S3BlobStoreProvider(TimeProvider.System, logs, static _ => throw new InvalidOperationException("No client may be built for a rejected definition."));

        var rejected = await denied.CreateAsync(Definition(WithProxy()), CancellationToken.None);

        // 定义不合法，客户端一个都没建；原因说明要运营方打开哪个开关，不带代理的任何取值
        Assert.Equal(ObjectStorageErrors.StoreDefinitionInvalid, rejected.Error?.Code);
        var reason = rejected.Error!.Details?["reason"]?.ToString() ?? string.Empty;
        Assert.Contains(S3BlobStoreProvider.AllowProxyKey, reason, StringComparison.Ordinal);
        var reported = rejected.Error.ToString() + reason;
        Assert.All(new[] { ProxyHost, ProxyUserName, ProxyPassword }, value => Assert.DoesNotContain(value, reported, StringComparison.Ordinal));
        Assert.All(logs.Entries, entry => Assert.All(new[] { ProxyHost, ProxyUserName, ProxyPassword }, value => Assert.DoesNotContain(value, entry.Message + entry.Exception, StringComparison.Ordinal)));

        // 运营方打开之后照常建出，设置原样交给建客户端的一步
        S3ObjectStorageOptions? seen = null;
        var allowed = new S3BlobStoreProvider(TimeProvider.System, NullLoggerFactory.Instance, options =>
        {
            seen = options;
            return FakeS3Client.Create().AsClient();
        }, ambientCredentials: false, allowProxy: true);
        var created = await allowed.CreateAsync(Definition(WithProxy()), CancellationToken.None);

        Assert.True(created.IsSuccess, created.ToString());
        Assert.Equal((ProxyHost, 3128), (seen?.ProxyHost, seen?.ProxyPort));
        await ((IAsyncDisposable)created.ValueOrThrow()).DisposeAsync();
    }

    [Fact]
    public async Task ANamedInstanceWithoutAProxy_NeedsNoSwitch()
    {
        var provider = new S3BlobStoreProvider(TimeProvider.System, NullLoggerFactory.Instance, static _ => FakeS3Client.Create().AsClient());

        var created = await provider.CreateAsync(Definition(), CancellationToken.None);

        Assert.True(created.IsSuccess, created.ToString());
        await ((IAsyncDisposable)created.ValueOrThrow()).DisposeAsync();
    }

    [Fact]
    public async Task TheProxySwitch_IsReadFromTheOperatorConfiguration_AndMustBeABoolean()
    {
        var addresses = new OutboundAddressPolicy(new OutboundAddressOptions(), NullLoggerFactory.Instance);
        Assert.Equal("XiHan:ObjectStorage:S3:NamedInstancesAllowProxy", S3BlobStoreProvider.AllowProxyKey);

        // 缺省（不写）与 false 都不许；不是布尔值时建 Provider 即抛出
        foreach (var configured in new string?[] { null, "false" })
        {
            var provider = new S3BlobStoreProvider(TimeProvider.System, NullLoggerFactory.Instance, addresses, Operator(configured));
            Assert.Equal(ObjectStorageErrors.StoreDefinitionInvalid, (await provider.CreateAsync(Definition(WithProxy()), CancellationToken.None)).Error?.Code);
        }

        var exception = Assert.Throws<ArgumentException>(() => new S3BlobStoreProvider(TimeProvider.System, NullLoggerFactory.Instance, addresses, Operator("maybe")));
        Assert.Contains(S3BlobStoreProvider.AllowProxyKey, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithTheSwitchOff_AProxyThatTheOutboundPolicyAllows_IsNeverConnected()
    {
        await using var proxy = RecordingHttpListener.Answering("502 Bad Gateway");
        var logs = new LogCapture();
        // 策略放行了回环，代理的地址本身能过；挡住它的是运营方的开关
        var provider = new S3BlobStoreProvider(TimeProvider.System, logs, new OutboundAddressPolicy(new OutboundAddressOptions { AllowLoopback = true }, logs), Operator(null));
        var settings = Settings(("ProxyHost", "127.0.0.1"), ("ProxyPort", proxy.Port.ToString(CultureInfo.InvariantCulture)));

        var created = await provider.CreateAsync(new BlobStoreDefinition("tenant-a", S3BlobStoreProvider.ProviderName, settings), CancellationToken.None);

        Assert.Equal(ObjectStorageErrors.StoreDefinitionInvalid, created.Error?.Code);
        Assert.Equal(0, proxy.ConnectionCount);
    }

    [Fact]
    public async Task TheSwitch_CannotBeTurnedOnFromTheInstanceSettings()
    {
        var provider = new S3BlobStoreProvider(TimeProvider.System, NullLoggerFactory.Instance, static _ => FakeS3Client.Create().AsClient());
        var settings = Settings([.. WithProxy(), ("NamedInstancesAllowProxy", "true")]);

        // 实例的设置里没有这个键：当成拼错的键拒绝，不会被读成开关
        var created = await provider.CreateAsync(new BlobStoreDefinition("tenant-a", S3BlobStoreProvider.ProviderName, settings), CancellationToken.None);

        Assert.Equal(ObjectStorageErrors.StoreDefinitionInvalid, created.Error?.Code);
        Assert.Contains("NamedInstancesAllowProxy", created.Error!.Details?["reason"]?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void TheDefaultInstance_MayStillUseAProxy()
    {
        // 缺省实例的配置节是运营方自己的，不受开关约束
        var options = S3ObjectStorageOptions.FromConfiguration(Configuration([.. WithProxy(), ("AccessKey", "ak"), ("SecretKey", "sk")]));

        Assert.Equal((ProxyHost, 3128), (options.ProxyHost, options.ProxyPort));
    }

    [Theory]
    [MemberData(nameof(InvalidServiceUrls))]
    public async Task AnInvalidServiceUrl_FailsNamingTheKey_WithoutEchoingIt(string serviceUrl)
    {
        var configured = Assert.Throws<ArgumentException>(() => S3ObjectStorageOptions.FromConfiguration(Configuration(("ServiceUrl", serviceUrl))));
        Assert.Contains($"'{S3ObjectStorageOptions.ConfigurationSection}:ServiceUrl'", configured.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(EndpointPassword, configured.Message, StringComparison.Ordinal);

        var settings = Assert.Throws<ArgumentException>(() => S3ObjectStorageOptions.FromSettings(new Dictionary<string, string> { ["ServiceUrl"] = serviceUrl }));
        Assert.Contains("'ServiceUrl'", settings.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(EndpointPassword, settings.Message, StringComparison.Ordinal);

        // 绑定的主值同样不回显，只说是哪个绑定
        var bound = Assert.Throws<ArgumentException>(() => S3ObjectStorageOptions.FromConfiguration(Configuration(), Maybe.Some(Binding(serviceUrl))));
        Assert.Contains($"'{S3ObjectStorageFeature.ConnectionBinding}'", bound.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(EndpointPassword, bound.Message, StringComparison.Ordinal);

        var direct = Assert.Throws<ArgumentException>(() => new S3ObjectStorageOptions { ServiceUrl = serviceUrl });
        Assert.DoesNotContain(EndpointPassword, direct.Message, StringComparison.Ordinal);

        // 命名实例返回的定义错误里也没有
        var provider = new S3BlobStoreProvider(TimeProvider.System, NullLoggerFactory.Instance, static _ => throw new InvalidOperationException("No client may be built for an invalid definition."));
        var created = await provider.CreateAsync(Definition(("ServiceUrl", serviceUrl)), CancellationToken.None);
        Assert.Equal(ObjectStorageErrors.StoreDefinitionInvalid, created.Error?.Code);
        Assert.DoesNotContain(EndpointPassword, created.Error!.ToString() + created.Error.Details?["reason"], StringComparison.Ordinal);
    }

    [Fact]
    public void AValidServiceUrl_IsKeptAsGiven()
    {
        // 合法的端点照旧原样保存；路径、查询与片段里的 @ 不是用户信息
        Assert.Equal("https://s3.example.com:9000/", S3ObjectStorageOptions.FromConfiguration(Configuration(("ServiceUrl", "https://s3.example.com:9000/"))).ServiceUrl);
        Assert.Equal("http://localhost:9000", S3ObjectStorageOptions.FromSettings(new Dictionary<string, string> { ["ServiceUrl"] = "http://localhost:9000" }).ServiceUrl);
        foreach (var url in new[] { "http://localhost:9000/a@b", "http://localhost:9000/?a=b@c", "http://localhost:9000/#a@b", "http://[::1]:9000" })
        {
            Assert.Equal(url, new S3ObjectStorageOptions { ServiceUrl = url }.ServiceUrl);
        }
    }

    [Theory]
    [MemberData(nameof(ServiceUrlsWithUserInformation))]
    public async Task AServiceUrlWithUserInformation_IsRejected_FromEverySource_WithoutEchoingIt(string serviceUrl)
    {
        // 缺省实例的配置节：报键名，指明凭据写在两项密钥里
        var configured = Assert.Throws<ArgumentException>(() => S3ObjectStorageOptions.FromConfiguration(Configuration(("ServiceUrl", serviceUrl))));
        Assert.Contains($"'{S3ObjectStorageOptions.ConfigurationSection}:ServiceUrl' must not contain user information", configured.Message, StringComparison.Ordinal);
        Assert.Contains($"'{S3ObjectStorageOptions.ConfigurationSection}:AccessKey'", configured.Message, StringComparison.Ordinal);
        AssertNotEchoed(configured.Message);

        // 绑定的主值：只说是哪个绑定，凭据走绑定的属性；绑定覆盖配置节，配置节里合法的端点救不了它
        var bound = Assert.Throws<ArgumentException>(() => S3ObjectStorageOptions.FromConfiguration(Configuration(("ServiceUrl", "http://localhost:9000")), Maybe.Some(Binding(serviceUrl, ("accessKey", "ak"), ("secretKey", "sk")))));
        Assert.Contains($"binding '{S3ObjectStorageFeature.ConnectionBinding}' must not contain user information", bound.Message, StringComparison.Ordinal);
        Assert.Contains("'accessKey'", bound.Message, StringComparison.Ordinal);
        AssertNotEchoed(bound.Message);

        // 命名实例的设置：读设置时拒绝，建实例返回定义不合法，客户端一个都不建
        var settings = Assert.Throws<ArgumentException>(() => S3ObjectStorageOptions.FromSettings(new Dictionary<string, string> { ["ServiceUrl"] = serviceUrl }));
        Assert.Contains("'ServiceUrl' must not contain user information", settings.Message, StringComparison.Ordinal);
        AssertNotEchoed(settings.Message);
        var logs = new LogCapture();
        var provider = new S3BlobStoreProvider(TimeProvider.System, logs, static _ => throw new InvalidOperationException("No client may be built for an invalid definition."));
        var created = await provider.CreateAsync(Definition(("ServiceUrl", serviceUrl)), CancellationToken.None);
        Assert.Equal(ObjectStorageErrors.StoreDefinitionInvalid, created.Error?.Code);
        AssertNotEchoed(created.Error!.ToString() + created.Error.Details?["reason"]);
        Assert.All(logs.Entries, static entry => AssertNotEchoed(entry.Message + entry.Exception));

        // 手工构造的选项同样拒绝
        var direct = Assert.Throws<ArgumentException>(() => new S3ObjectStorageOptions { ServiceUrl = serviceUrl });
        Assert.Contains("must not contain user information", direct.Message, StringComparison.Ordinal);
        AssertNotEchoed(direct.Message);
    }

    [Fact]
    public void TheFeature_FailsToConfigure_WithUserInformationInTheServiceUrl()
    {
        // 缺省实例装配失败：照成组设置的处置，抛出 ArgumentException，不带原值
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
        [
            new("XiHan:ObjectStorage:S3:ServiceUrl", $"http://admin:{EndpointPassword}@localhost:9000"),
            new("XiHan:ObjectStorage:S3:ConditionalWrites", "false"),
        ]).Build();
        var builder = new AppBuilder(new ServiceCollection(), configuration).UseFeature<S3ObjectStorageFeature>();

        var exception = Assert.ThrowsAny<Exception>(() => builder.Build());

        Assert.Contains("'XiHan:ObjectStorage:S3:ServiceUrl' must not contain user information", exception.ToString(), StringComparison.Ordinal);
        AssertNotEchoed(exception.ToString());
    }

    [Theory]
    [MemberData(nameof(OneSidedKeys))]
    public void OneKeyAlone_FailsForTheDefaultInstanceToo(string key, string value)
    {
        // 以前缺省实例只给一项时 SDK 忽略它、悄悄走默认凭据链；现在与命名实例一样拒绝，消息只写键名
        var exception = Assert.Throws<ArgumentException>(() => S3ObjectStorageOptions.FromConfiguration(Configuration(("ServiceUrl", "http://localhost:9000"), (key, value))));

        Assert.Contains($"'{S3ObjectStorageOptions.ConfigurationSection}:AccessKey'", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"'{S3ObjectStorageOptions.ConfigurationSection}:SecretKey'", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(value, exception.Message, StringComparison.Ordinal);

        // 手工构造的选项在建客户端时同样被拒
        var built = key == "AccessKey" ? new S3ObjectStorageOptions { AccessKey = value } : new S3ObjectStorageOptions { SecretKey = value };
        Assert.Throws<ArgumentException>(() => S3ObjectStorageFeature.CreateCredentials(built));
        Assert.Throws<ArgumentException>(() => S3ObjectStorageFeature.CreateClientConfig(built, null));
    }

    [Fact]
    public void TheKeyPair_IsCheckedAfterTheBindingOverrides()
    {
        // 一项来自配置节、一项来自绑定：覆盖之后是完整的一对
        var mixed = S3ObjectStorageOptions.FromConfiguration(Configuration(("AccessKey", "ak")), Maybe.Some(Binding(null, ("secretKey", "bound-sk"))));
        Assert.Equal(("ak", "bound-sk"), (mixed.AccessKey, mixed.SecretKey));

        // 绑定补不齐时照样拒绝：配置节与绑定各自都只有访问密钥 ID
        Assert.Throws<ArgumentException>(() => S3ObjectStorageOptions.FromConfiguration(Configuration(("AccessKey", "ak")), Maybe.Some(Binding(null, ("accessKey", "bound-ak")))));

        // 两项都没有仍是默认凭据链
        Assert.Null(S3ObjectStorageFeature.CreateCredentials(S3ObjectStorageOptions.FromConfiguration(Configuration(("ServiceUrl", "http://localhost:9000")))));
    }

    [Fact]
    public void TheFeature_FailsToConfigure_WithOneKeyAlone()
    {
        // 缺省实例装配失败：照 ADR-0281 成组设置的处置，抛出 ArgumentException
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
        [
            new("XiHan:ObjectStorage:S3:ServiceUrl", "http://localhost:9000"),
            new("XiHan:ObjectStorage:S3:AccessKey", "ak"),
            new("XiHan:ObjectStorage:S3:ConditionalWrites", "false"),
        ]).Build();
        var builder = new AppBuilder(new ServiceCollection(), configuration).UseFeature<S3ObjectStorageFeature>();

        var exception = Assert.ThrowsAny<Exception>(() => builder.Build());

        Assert.Contains("'XiHan:ObjectStorage:S3:SecretKey'", exception.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 核对文本里没有端点里的用户名与口令
    /// </summary>
    private static void AssertNotEchoed(string text)
    {
        Assert.DoesNotContain(EndpointUser, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(EndpointPassword, text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 配了代理（含凭据）的一组设置
    /// </summary>
    private static (string Key, string Value)[] WithProxy() =>
    [
        ("ProxyHost", ProxyHost),
        ("ProxyPort", "3128"),
        ("ProxyUserName", ProxyUserName),
        ("ProxyPassword", ProxyPassword),
    ];

    /// <summary>
    /// 带着两项密钥、不探测条件写入的命名实例定义
    /// </summary>
    private static BlobStoreDefinition Definition(params (string Key, string Value)[] settings)
        => new("tenant-a", S3BlobStoreProvider.ProviderName, Settings(settings));

    /// <summary>
    /// 命名实例的设置：给定的键值加上两项密钥与关闭的条件写入
    /// </summary>
    private static Dictionary<string, string> Settings(params (string Key, string Value)[] settings)
    {
        var all = new Dictionary<string, string>(StringComparer.Ordinal) { ["AccessKey"] = "ak", ["SecretKey"] = "sk", ["ConditionalWrites"] = "false" };
        foreach (var (key, value) in settings)
        {
            all[key] = value;
        }

        return all;
    }

    /// <summary>
    /// 运营方的配置：代理开关取给定的值，为 <see langword="null"/> 时不写
    /// </summary>
    private static IConfiguration Operator(string? allowProxy)
        => new ConfigurationBuilder().AddInMemoryCollection(allowProxy is null ? [] : [new KeyValuePair<string, string?>(S3BlobStoreProvider.AllowProxyKey, allowProxy)]).Build();

    /// <summary>
    /// 以配置节下的键值构造内存配置
    /// </summary>
    private static IConfiguration Configuration(params (string Key, string Value)[] settings)
        => new ConfigurationBuilder().AddInMemoryCollection(settings.Select(static pair => new KeyValuePair<string, string?>($"{S3ObjectStorageOptions.ConfigurationSection}:{pair.Key}", pair.Value))).Build();

    /// <summary>
    /// 连接绑定 <c>objectstorage</c>
    /// </summary>
    private static BindingValue Binding(string? value, params (string Key, string Value)[] properties) => new()
    {
        Name = S3ObjectStorageFeature.ConnectionBinding,
        Type = "blob",
        Value = value,
        Properties = properties.ToDictionary(static p => p.Key, static p => p.Value, StringComparer.Ordinal),
    };
}
