// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Configuration.Abstractions;
using XiHan.Framework.Configuration.Abstractions.Remote;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Configuration.Nacos.Tests;

/// <summary>
/// Nacos Provider 独有的行为。
/// </summary>
/// <remarks>
/// 选项解析与绑定、文档展平、格式不合法的错误、properties 格式、版本即内容 MD5（HTTP 桩）、接进配置构建器、特性在宿主里的装配。一致性套件覆盖的通用行为不在此重复。
/// </remarks>
public sealed class NacosConfigurationTests
{
    /// <summary>
    /// 用键值对构造内存配置。
    /// </summary>
    private static IConfiguration Build(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    [Fact]
    public void Options_FromConfiguration_ParsesAndRejectsInvalid()
    {
        var binding = Maybe.Some(new BindingValue("nacos", "configuration:remote", "http://bound:8848", new Dictionary<string, string>()));
        var bound = NacosOptions.FromConfiguration(Build(), binding);
        Assert.Equal(["http://bound:8848"], bound.ServerAddresses);
        Assert.Equal("DEFAULT_GROUP", bound.Group);
        Assert.Equal(string.Empty, bound.Tenant);
        Assert.Equal(NacosConfigFormat.Json, bound.Format);
        Assert.Equal(TimeSpan.FromSeconds(30), bound.LongPollTimeout);

        var configured = NacosOptions.FromConfiguration(Build(
            ("XiHan:Configuration:Nacos:ServerAddresses", "http://a:8848, http://b:8848;http://c:8848"),
            ("XiHan:Configuration:Nacos:Group", "orders"),
            ("XiHan:Configuration:Nacos:Tenant", "dev"),
            ("XiHan:Configuration:Nacos:Username", "nacos"),
            ("XiHan:Configuration:Nacos:Password", "secret"),
            ("XiHan:Configuration:Nacos:Format", "properties"),
            ("XiHan:Configuration:Nacos:LongPollTimeoutSeconds", "5"),
            ("XiHan:Configuration:Nacos:RequestTimeoutSeconds", "2.5")), binding);
        Assert.Equal(["http://a:8848", "http://b:8848", "http://c:8848"], configured.ServerAddresses);
        Assert.Equal(("orders", "dev", "nacos", "secret"), (configured.Group, configured.Tenant, configured.Username, configured.Password));
        Assert.Equal(NacosConfigFormat.Properties, configured.Format);
        Assert.Equal(TimeSpan.FromSeconds(5), configured.LongPollTimeout);
        Assert.Equal(TimeSpan.FromSeconds(2.5), configured.RequestTimeout);

        Assert.Throws<ArgumentException>(() => NacosOptions.FromConfiguration(Build(), default));
        Assert.Throws<ArgumentException>(static () => NacosOptions.FromConfiguration(Build(("XiHan:Configuration:Nacos:ServerAddresses", "nacos:8848"))));
        Assert.Throws<ArgumentException>(static () => NacosOptions.FromConfiguration(Build(("XiHan:Configuration:Nacos:ServerAddresses", "http://a:8848"), ("XiHan:Configuration:Nacos:Format", "yaml"))));
        Assert.Throws<ArgumentException>(static () => new NacosOptions { ServerAddresses = [] });
    }

    [Fact]
    public void Parser_FlattensJson_AndParsesProperties()
    {
        var json = NacosDocumentParser.Parse("""{"Greeting":"hello","Nested":{"Count":2,"On":true},"List":["a","b"],"Empty":null}""", NacosConfigFormat.Json);
        Assert.Equal("hello", json["Greeting"]);
        Assert.Equal("2", json["Nested:Count"]);
        Assert.Equal("True", json["Nested:On"]);
        Assert.Equal("a", json["List:0"]);
        Assert.Equal("b", json["List:1"]);
        Assert.True(json.ContainsKey("Empty"));
        Assert.Null(json["Empty"]);

        var properties = NacosDocumentParser.Parse("# comment\n! another\nGreeting = hello\nNested.Count:2\nflag\n", NacosConfigFormat.Properties);
        Assert.Equal("hello", properties["Greeting"]);
        Assert.Equal("2", properties["Nested:Count"]);
        Assert.Equal(string.Empty, properties["flag"]);

        Assert.Throws<FormatException>(static () => NacosDocumentParser.Parse("not json", NacosConfigFormat.Json));
        Assert.Throws<FormatException>(static () => NacosDocumentParser.Parse("[1,2]", NacosConfigFormat.Json));
        Assert.Throws<FormatException>(static () => NacosDocumentParser.Parse("=value", NacosConfigFormat.Properties));
    }

    [Fact]
    public async Task Fetch_InvalidDocument_IsValidationError_AndPropertiesFormatWorks()
    {
        var dataId = "xihan-test-" + Guid.NewGuid().ToString("N")[..8];
        await NacosTestServer.PublishRawAsync(dataId, "this is not json", "text", CancellationToken.None);
        using var json = NacosConfigurationClient.Create(NacosTestServer.Options());
        var invalid = await json.FetchAsync(dataId, null, CancellationToken.None);
        Assert.Equal(ConfigurationErrors.DocumentInvalid, invalid.Error?.Code);

        await NacosTestServer.PublishRawAsync(dataId, "Greeting=hello\nNested.Count=2\n", "properties", CancellationToken.None);
        using var properties = NacosConfigurationClient.Create(NacosTestServer.Options(NacosConfigFormat.Properties));
        var document = (await properties.FetchAsync(dataId, null, CancellationToken.None)).ValueOrThrow().ValueOrThrow();
        Assert.Equal("hello", document.Values["Greeting"]);
        Assert.Equal("2", document.Values["Nested:Count"]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Fetch_VersionIsTheContentMd5_SoListeningRecognisesIt(bool header)
    {
        // Nacos 按 UTF-8 计算内容 MD5，监听时拿报来的值逐字比较；期望值由独立工具算出，含非 ASCII 字符以覆盖编码
        const string Document = """{"Greeting":"你好"}""";
        const string DocumentMd5 = "f54abb65c17e7fa3db719d8636acdc25";
        // 有响应头是 Nacos 的常态（HttpClient 把它归入内容头），没有时（如经过去掉它的代理）客户端自己算
        using var handler = new NacosStubHandler { Content = Document, ContentMd5 = DocumentMd5, SendContentMd5Header = header };
        using var http = new HttpClient(handler, disposeHandler: false);
        using var client = new NacosConfigurationClient(new NacosOptions { ServerAddresses = ["http://nacos.test:8848"] }, http, TimeProvider.System);

        var document = (await client.FetchAsync("orders", null, CancellationToken.None)).ValueOrThrow().ValueOrThrow();
        Assert.Equal(DocumentMd5, document.Version);
        Assert.Equal("你好", document.Values["Greeting"]);

        // 以这个版本监听：服务端认出未变化，返回无值，不再取整份文档
        var unchanged = await client.FetchAsync("orders", document.Version, CancellationToken.None);
        Assert.False(unchanged.ValueOrThrow().HasValue);
        Assert.Equal(DocumentMd5, Assert.Single(handler.ListenedMd5));
        Assert.Equal(1, handler.Fetches);
    }

    [Fact]
    public async Task Fetch_ContentMd5Header_IsTakenAsIs()
    {
        // 响应头给出的值原样作版本，不按内容重算
        using var handler = new NacosStubHandler { Content = """{"Greeting":"hello"}""", ContentMd5 = "0123456789abcdef0123456789abcdef", SendContentMd5Header = true };
        using var http = new HttpClient(handler, disposeHandler: false);
        using var client = new NacosConfigurationClient(new NacosOptions { ServerAddresses = ["http://nacos.test:8848"] }, http, TimeProvider.System);

        var document = (await client.FetchAsync("orders", null, CancellationToken.None)).ValueOrThrow().ValueOrThrow();

        Assert.Equal("0123456789abcdef0123456789abcdef", document.Version);
    }

    [Fact]
    public async Task ConfigurationBuilder_LoadsDocument_UnderPrefix()
    {
        var dataId = "xihan-test-" + Guid.NewGuid().ToString("N")[..8];
        await NacosTestServer.PublishAsync(dataId, new Dictionary<string, string?> { ["Greeting"] = "hello" }, CancellationToken.None);

        var configuration = new ConfigurationBuilder()
            .AddXiHanNacos(NacosTestServer.Options(), new RemoteConfigurationOptions(dataId) { Prefix = "Orders", PollInterval = null })
            .Build();

        Assert.Equal("hello", configuration["Orders:Greeting"]);
    }

    [Fact]
    public async Task Feature_RegistersClient_AsRemoteConfiguration()
    {
        var url = NacosTestServer.RequireServiceUrl();
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<NacosConfigurationFeature>(), [new("XiHan:Configuration:Nacos:ServerAddresses", url)]);

        Assert.Contains("xihan.configuration.nacos", host.App.DescribeGraph(), StringComparison.Ordinal);
        var client = Assert.IsType<NacosConfigurationClient>(host.Services.GetRequiredService<IRemoteConfigurationClient>());
        Assert.Equal(ConfigurationErrors.NamespaceNotFound, (await client.FetchAsync("xihan-test-missing-" + Guid.NewGuid().ToString("N")[..8], null, CancellationToken.None)).Error?.Code);
    }

    [Fact]
    public async Task Fetch_UnreachableServer_IsUnavailable()
    {
        using var client = NacosConfigurationClient.Create(new NacosOptions { ServerAddresses = ["http://127.0.0.1:1"], RequestTimeout = TimeSpan.FromSeconds(2) });

        var result = await client.FetchAsync("anything", null, CancellationToken.None);

        Assert.Equal(ConfigurationErrors.RemoteUnavailable, result.Error?.Code);
    }
}
