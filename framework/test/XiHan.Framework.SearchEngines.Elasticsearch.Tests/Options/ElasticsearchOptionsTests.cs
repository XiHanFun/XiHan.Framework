// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.SearchEngines.Elasticsearch.Options;

namespace XiHan.Framework.SearchEngines.Elasticsearch.Tests.Options;

/// <summary>
/// Elasticsearch 配置的测试
/// </summary>
/// <remarks>
/// 这个类是纯配置载体，没有 Validate 方法，所以它的契约全落在两处：默认值与属性名。
/// 默认值决定「什么都不配就能连本机集群」这条开箱体验；属性名是 appsettings 的绑定键，
/// 改名不会有任何编译错误，只会让线上配置静默失效回退到默认值。两者都在这里锁死。
/// </remarks>
public class ElasticsearchOptionsTests
{
    /// <summary>
    /// 不做任何配置时取开箱默认值
    /// </summary>
    /// <remarks>
    /// 地址默认指向本机 9200，认证三项默认为空表示匿名访问，分片与副本各 1。
    /// </remarks>
    [Fact]
    public void Constructor_Default_UsesDocumentedDefaults()
    {
        var options = new ElasticsearchOptions();

        Assert.Equal("http://localhost:9200", options.Uri);
        Assert.Null(options.UserName);
        Assert.Null(options.Password);
        Assert.Null(options.ApiKey);
        Assert.Equal(string.Empty, options.IndexPrefix);
        Assert.Equal(30, options.RequestTimeoutSeconds);
        Assert.False(options.AllowUntrustedCertificate);
        Assert.Equal(1, options.NumberOfShards);
        Assert.Equal(1, options.NumberOfReplicas);
    }

    /// <summary>
    /// 索引前缀默认是空串而不是空引用
    /// </summary>
    /// <remarks>
    /// 引擎用 <c>string.IsNullOrEmpty(IndexPrefix)</c> 判断要不要拼前缀，空引用虽然也走得通，
    /// 但会让配置导出、日志打印里的「未配置」与「配置成空」两种状态表现不一致。
    /// </remarks>
    [Fact]
    public void IndexPrefix_Default_IsEmptyStringNotNull()
    {
        var options = new ElasticsearchOptions();

        Assert.NotNull(options.IndexPrefix);
        Assert.Equal(0, options.IndexPrefix.Length);
    }

    /// <summary>
    /// 配置节名称不随重构漂移
    /// </summary>
    /// <remarks>
    /// 这是外部 appsettings.json 的键路径，改动会让所有既有部署的配置一起失效且不报错。
    /// </remarks>
    [Fact]
    public void SectionName_IsStableConfigurationPath()
    {
        Assert.Equal("XiHan:SearchEngines:Elasticsearch", ElasticsearchOptions.SectionName);
    }

    /// <summary>
    /// 各配置项均可写入并原样读回
    /// </summary>
    [Fact]
    public void Properties_WhenAssigned_RoundTrip()
    {
        var options = new ElasticsearchOptions
        {
            Uri = "https://es.example.com:9243",
            UserName = "elastic",
            Password = "changeme",
            ApiKey = "api-key-value",
            IndexPrefix = "tenant-a-",
            RequestTimeoutSeconds = 5,
            AllowUntrustedCertificate = true,
            NumberOfShards = 3,
            NumberOfReplicas = 2
        };

        Assert.Equal("https://es.example.com:9243", options.Uri);
        Assert.Equal("elastic", options.UserName);
        Assert.Equal("changeme", options.Password);
        Assert.Equal("api-key-value", options.ApiKey);
        Assert.Equal("tenant-a-", options.IndexPrefix);
        Assert.Equal(5, options.RequestTimeoutSeconds);
        Assert.True(options.AllowUntrustedCertificate);
        Assert.Equal(3, options.NumberOfShards);
        Assert.Equal(2, options.NumberOfReplicas);
    }

    /// <summary>
    /// 从配置节按属性名绑定出全部配置项
    /// </summary>
    /// <remarks>
    /// 锁的是「配置键 = 属性名」这条对外契约，任一属性改名都会让本用例失败。
    /// </remarks>
    [Fact]
    public void Bind_FromConfigurationSection_MapsEveryKey()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ElasticsearchOptions.SectionName}:Uri"] = "https://es.example.com:9243",
                [$"{ElasticsearchOptions.SectionName}:UserName"] = "elastic",
                [$"{ElasticsearchOptions.SectionName}:Password"] = "changeme",
                [$"{ElasticsearchOptions.SectionName}:ApiKey"] = "api-key-value",
                [$"{ElasticsearchOptions.SectionName}:IndexPrefix"] = "tenant-a-",
                [$"{ElasticsearchOptions.SectionName}:RequestTimeoutSeconds"] = "5",
                [$"{ElasticsearchOptions.SectionName}:AllowUntrustedCertificate"] = "true",
                [$"{ElasticsearchOptions.SectionName}:NumberOfShards"] = "3",
                [$"{ElasticsearchOptions.SectionName}:NumberOfReplicas"] = "2"
            })
            .Build();

        var options = new ElasticsearchOptions();
        configuration.GetSection(ElasticsearchOptions.SectionName).Bind(options);

        Assert.Equal("https://es.example.com:9243", options.Uri);
        Assert.Equal("elastic", options.UserName);
        Assert.Equal("changeme", options.Password);
        Assert.Equal("api-key-value", options.ApiKey);
        Assert.Equal("tenant-a-", options.IndexPrefix);
        Assert.Equal(5, options.RequestTimeoutSeconds);
        Assert.True(options.AllowUntrustedCertificate);
        Assert.Equal(3, options.NumberOfShards);
        Assert.Equal(2, options.NumberOfReplicas);
    }

    /// <summary>
    /// 配置节只给部分键时其余项保留默认值
    /// </summary>
    /// <remarks>
    /// 常见部署只改地址与认证，分片副本沿用默认；若绑定把未出现的键覆盖成零值，
    /// 创建索引会带着 0 分片发出去而不是用默认的 1。
    /// </remarks>
    [Fact]
    public void Bind_WhenSectionPartial_KeepsDefaults()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ElasticsearchOptions.SectionName}:Uri"] = "http://es-node:9200"
            })
            .Build();

        var options = new ElasticsearchOptions();
        configuration.GetSection(ElasticsearchOptions.SectionName).Bind(options);

        Assert.Equal("http://es-node:9200", options.Uri);
        Assert.Equal(30, options.RequestTimeoutSeconds);
        Assert.Equal(1, options.NumberOfShards);
        Assert.Equal(1, options.NumberOfReplicas);
        Assert.Equal(string.Empty, options.IndexPrefix);
        Assert.Null(options.ApiKey);
        Assert.Null(options.UserName);
    }

    /// <summary>
    /// 配置里没有该节时绑定不改动任何一项
    /// </summary>
    [Fact]
    public void Bind_WhenSectionAbsent_LeavesDefaults()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["XiHan:SearchEngines:SomethingElse:Uri"] = "http://wrong-node:9200"
            })
            .Build();

        var options = new ElasticsearchOptions();
        configuration.GetSection(ElasticsearchOptions.SectionName).Bind(options);

        Assert.Equal("http://localhost:9200", options.Uri);
        Assert.Equal(30, options.RequestTimeoutSeconds);
        Assert.Equal(1, options.NumberOfShards);
        Assert.Equal(1, options.NumberOfReplicas);
    }

    /// <summary>
    /// 超出常规范围的数值在配置层原样保留
    /// </summary>
    /// <param name="value">数值</param>
    /// <remarks>
    /// 本类不做校验，超时、分片、副本的合法性由 Elasticsearch 在请求期裁定。
    /// 用例锁的是「配置层不吞不改」：一旦有人在此处加静默纠偏（比如把 0 悄悄改成 1），
    /// 错误配置就会在测试环境看起来一切正常，到生产才以另一种形式暴露。
    /// </remarks>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void NumericProperties_WhenOutOfTypicalRange_AreKeptAsIs(int value)
    {
        var options = new ElasticsearchOptions
        {
            RequestTimeoutSeconds = value,
            NumberOfShards = value,
            NumberOfReplicas = value
        };

        Assert.Equal(value, options.RequestTimeoutSeconds);
        Assert.Equal(value, options.NumberOfShards);
        Assert.Equal(value, options.NumberOfReplicas);
    }
}
