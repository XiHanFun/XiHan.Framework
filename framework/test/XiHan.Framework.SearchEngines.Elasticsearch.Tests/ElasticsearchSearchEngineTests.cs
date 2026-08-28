// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.SearchEngines.Abstractions;
using XiHan.Framework.SearchEngines.Abstractions.Documents;
using XiHan.Framework.SearchEngines.Abstractions.Querying;
using XiHan.Framework.SearchEngines.Elasticsearch.Options;

namespace XiHan.Framework.SearchEngines.Elasticsearch.Tests;

/// <summary>
/// Elasticsearch 搜索引擎的测试
/// </summary>
/// <remarks>
/// 需要真实集群才有意义的行为已由 XiHan.Framework.SearchEngines.Tests 里的
/// <c>ElasticsearchSearchEngineContractTests</c> 覆盖（集群不可达时整类跳过），这里刻意不重复，
/// 只测走不到网络上的那一层：构造期的客户端装配，以及每个方法在发出请求之前的参数校验。
/// <para>
/// 所有用例把节点地址设为 <c>http://127.0.0.1:1</c>。这不是随手写的占位值：万一某条断言的
/// 前提判断错了、真的发出了请求，1 号端口不会有服务监听，会立刻连接被拒而不是挂在超时上，
/// 用例因此失败得快且明确，不会拖住流水线；同时也保证本套用例绝不会误打到本机的真实集群。
/// </para>
/// </remarks>
public class ElasticsearchSearchEngineTests
{
    /// <summary>
    /// 不会有服务监听的节点地址
    /// </summary>
    private const string UnusedNode = "http://127.0.0.1:1";

    /// <summary>
    /// 引擎按标记接口暴露契约与单例生命周期
    /// </summary>
    /// <remarks>
    /// 框架按 <c>ISingletonDependency</c> 做约定式注册。传输层连接池与索引校验缓存都是实例级状态，
    /// 生命周期一旦掉成瞬时，每次调用都会重建连接池并丢掉缓存，退化成每写一条文档多一次 HEAD 请求。
    /// </remarks>
    [Fact]
    public void Type_DeclaresSearchEngineContractAndSingletonLifetime()
    {
        Assert.True(typeof(ISearchEngine).IsAssignableFrom(typeof(ElasticsearchSearchEngine)));
        Assert.True(typeof(ISingletonDependency).IsAssignableFrom(typeof(ElasticsearchSearchEngine)));
        Assert.True(typeof(ElasticsearchSearchEngine).IsSealed);
    }

    /// <summary>
    /// 节点不可达时构造依然成功，说明构造期不探测服务端
    /// </summary>
    /// <remarks>
    /// 单例在容器启动时就被构造。如果构造期做同步握手，集群短暂不可用会直接拖垮整个应用启动，
    /// 而不是只让搜索功能降级。这里连到一个必定拒绝连接的端口，构造仍不抛异常即证明连接是惰性的。
    /// </remarks>
    [Fact]
    public void Constructor_WhenNodeUnreachable_DoesNotProbeServer()
    {
        var engine = CreateEngine();

        Assert.IsAssignableFrom<ISearchEngine>(engine);
    }

    /// <summary>
    /// 各认证配置组合都能装配出客户端
    /// </summary>
    /// <param name="apiKey">API 密钥</param>
    /// <param name="userName">用户名</param>
    /// <param name="password">密码</param>
    /// <remarks>
    /// 三条分支互斥：有 API 密钥走密钥认证，否则有用户名走基础认证，都没有则匿名。
    /// 密钥为纯空白等同于没配（实现用的是 IsNullOrWhiteSpace 判断）；
    /// 只给用户名不给密码时密码回退成空串而不是抛空引用——这条最容易在只配了用户名的环境里炸。
    /// </remarks>
    [Theory]
    [InlineData(null, null, null)]
    [InlineData("api-key-value", null, null)]
    [InlineData(null, "elastic", "changeme")]
    [InlineData(null, "elastic", null)]
    [InlineData("   ", "elastic", "changeme")]
    [InlineData("api-key-value", "elastic", "changeme")]
    public void Constructor_WithAuthenticationVariants_BuildsClient(string? apiKey, string? userName, string? password)
    {
        var engine = CreateEngine(options =>
        {
            options.ApiKey = apiKey;
            options.UserName = userName;
            options.Password = password;
        });

        Assert.IsAssignableFrom<ISearchEngine>(engine);
    }

    /// <summary>
    /// 跳过证书校验的开关不影响客户端装配
    /// </summary>
    /// <param name="allowUntrusted">是否跳过服务端证书校验</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_WithCertificateValidationSwitch_BuildsClient(bool allowUntrusted)
    {
        var engine = CreateEngine(options => options.AllowUntrustedCertificate = allowUntrusted);

        Assert.IsAssignableFrom<ISearchEngine>(engine);
    }

    /// <summary>
    /// 节点地址不是合法绝对 URI 时构造期直接抛出
    /// </summary>
    /// <param name="uri">节点地址</param>
    /// <remarks>
    /// 地址配错属于启动即错，必须在构造期炸掉。若拖到第一次检索才报，
    /// 现场看到的是一个连接类异常，排查方向会被带偏到网络与集群上。
    /// </remarks>
    [Theory]
    [InlineData("")]
    [InlineData("not a uri")]
    public void Constructor_WhenUriMalformed_ThrowsUriFormatException(string uri)
    {
        Assert.Throws<UriFormatException>(() => CreateEngine(options => options.Uri = uri));
    }

    /// <summary>
    /// 节点地址为空引用时构造期抛出空引用参数异常
    /// </summary>
    [Fact]
    public void Constructor_WhenUriNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CreateEngine(options => options.Uri = null!));
    }

    /// <summary>
    /// 判断索引是否存在时索引名为空白，在发出请求之前抛出参数异常
    /// </summary>
    /// <param name="index">索引名</param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public async Task IndexExistsAsync_WhenIndexBlank_ThrowsArgumentException(string index)
    {
        var engine = CreateEngine();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => engine.IndexExistsAsync(index, TestContext.Current.CancellationToken));

        Assert.Equal("index", exception.ParamName);
    }

    /// <summary>
    /// 判断索引是否存在时索引名为空引用，抛出空引用参数异常
    /// </summary>
    [Fact]
    public async Task IndexExistsAsync_WhenIndexNull_ThrowsArgumentNullException()
    {
        var engine = CreateEngine();

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => engine.IndexExistsAsync(null!, TestContext.Current.CancellationToken));

        Assert.Equal("index", exception.ParamName);
    }

    /// <summary>
    /// 配了索引前缀也不会让空白索引名蒙混过关
    /// </summary>
    /// <remarks>
    /// 校验发生在拼前缀之前。顺序若反过来，空白索引名会被前缀补成一个合法名字，
    /// 请求就悄悄打到以前缀命名的那个索引上，且全程不报错。
    /// </remarks>
    [Fact]
    public async Task IndexExistsAsync_WhenIndexBlankAndPrefixConfigured_StillThrows()
    {
        var engine = CreateEngine(options => options.IndexPrefix = "tenant-a-");

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => engine.IndexExistsAsync(" ", TestContext.Current.CancellationToken));

        Assert.Equal("index", exception.ParamName);
    }

    /// <summary>
    /// 删除索引时索引名为空白，在发出请求之前抛出参数异常
    /// </summary>
    /// <param name="index">索引名</param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task DeleteIndexAsync_WhenIndexBlank_ThrowsArgumentException(string index)
    {
        var engine = CreateEngine();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => engine.DeleteIndexAsync(index, TestContext.Current.CancellationToken));

        Assert.Equal("index", exception.ParamName);
    }

    /// <summary>
    /// 刷新索引时索引名为空白，在发出请求之前抛出参数异常
    /// </summary>
    /// <param name="index">索引名</param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task RefreshAsync_WhenIndexBlank_ThrowsArgumentException(string index)
    {
        var engine = CreateEngine();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => engine.RefreshAsync(index, TestContext.Current.CancellationToken));

        Assert.Equal("index", exception.ParamName);
    }

    /// <summary>
    /// 删除文档时索引名为空白，在发出请求之前抛出参数异常
    /// </summary>
    /// <param name="index">索引名</param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task DeleteAsync_WhenIndexBlank_ThrowsArgumentException(string index)
    {
        var engine = CreateEngine();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => engine.DeleteAsync(index, "1", TestContext.Current.CancellationToken));

        Assert.Equal("index", exception.ParamName);
    }

    /// <summary>
    /// 读取文档时索引名为空白，在发出请求之前抛出参数异常
    /// </summary>
    /// <param name="index">索引名</param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GetAsync_WhenIndexBlank_ThrowsArgumentException(string index)
    {
        var engine = CreateEngine();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => engine.GetAsync<TestArticle>(index, "1", TestContext.Current.CancellationToken));

        Assert.Equal("index", exception.ParamName);
    }

    /// <summary>
    /// 写入文档时索引名为空白，在发出请求之前抛出参数异常
    /// </summary>
    /// <remarks>
    /// 写入路径会先做一次索引存在性校验，空白索引名必须在那一步之前就被拦下，
    /// 而不是拿一个空名字去问服务端。
    /// </remarks>
    [Fact]
    public async Task IndexAsync_WhenIndexBlank_ThrowsArgumentException()
    {
        var engine = CreateEngine();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => engine.IndexAsync(string.Empty, NewDocument("1"), TestContext.Current.CancellationToken));

        Assert.Equal("index", exception.ParamName);
    }

    /// <summary>
    /// 批量写入时索引名为空白，在发出请求之前抛出参数异常
    /// </summary>
    [Fact]
    public async Task IndexManyAsync_WhenIndexBlank_ThrowsArgumentException()
    {
        var engine = CreateEngine();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => engine.IndexManyAsync<TestArticle>(string.Empty, [NewDocument("1")], TestContext.Current.CancellationToken));

        Assert.Equal("index", exception.ParamName);
    }

    /// <summary>
    /// 创建索引时定义为空引用，抛出空引用参数异常
    /// </summary>
    [Fact]
    public async Task CreateIndexAsync_WhenDefinitionNull_ThrowsArgumentNullException()
    {
        var engine = CreateEngine();

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => engine.CreateIndexAsync(null!, TestContext.Current.CancellationToken));

        Assert.Equal("definition", exception.ParamName);
    }

    /// <summary>
    /// 写入文档时文档为空引用，抛出空引用参数异常
    /// </summary>
    /// <remarks>
    /// 这条校验排在索引存在性校验之前，所以整条路径不会产生任何请求。
    /// </remarks>
    [Fact]
    public async Task IndexAsync_WhenDocumentNull_ThrowsArgumentNullException()
    {
        var engine = CreateEngine();

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => engine.IndexAsync<TestArticle>("articles", null!, TestContext.Current.CancellationToken));

        Assert.Equal("document", exception.ParamName);
    }

    /// <summary>
    /// 批量写入时文档集合为空引用，抛出空引用参数异常
    /// </summary>
    [Fact]
    public async Task IndexManyAsync_WhenDocumentsNull_ThrowsArgumentNullException()
    {
        var engine = CreateEngine();

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => engine.IndexManyAsync<TestArticle>("articles", null!, TestContext.Current.CancellationToken));

        Assert.Equal("documents", exception.ParamName);
    }

    /// <summary>
    /// 检索时请求为空引用，抛出空引用参数异常
    /// </summary>
    [Fact]
    public async Task SearchAsync_WhenRequestNull_ThrowsArgumentNullException()
    {
        var engine = CreateEngine();

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => engine.SearchAsync<TestArticle>(null!, TestContext.Current.CancellationToken));

        Assert.Equal("request", exception.ParamName);
    }

    /// <summary>
    /// 过滤运算符超出已知取值时抛出不支持异常
    /// </summary>
    /// <remarks>
    /// 查询体是在发请求之前拼好的，因此翻译不出来的运算符会在本地立即暴露，
    /// 而不是拼成一个残缺查询体发给服务端、换回一个语焉不详的 400。
    /// 用强转构造出枚举取值之外的运算符，模拟抽象层新增了运算符而本实现尚未跟进的情形。
    /// </remarks>
    [Fact]
    public async Task SearchAsync_WhenFilterOperatorUnsupported_ThrowsNotSupportedException()
    {
        var engine = CreateEngine();
        var request = new SearchRequest("articles")
        {
            Filters = [new SearchFilter("category", (SearchFilterOperator)999, "framework")]
        };

        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            () => engine.SearchAsync<TestArticle>(request, TestContext.Current.CancellationToken));

        Assert.Contains("不支持的过滤运算符", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 创建指向不可达节点的引擎
    /// </summary>
    /// <param name="configure">在默认配置之上追加的改动</param>
    /// <returns>引擎</returns>
    private static ElasticsearchSearchEngine CreateEngine(Action<ElasticsearchOptions>? configure = null)
    {
        var options = new ElasticsearchOptions
        {
            Uri = UnusedNode,
            RequestTimeoutSeconds = 1
        };

        configure?.Invoke(options);

        return new ElasticsearchSearchEngine(new FixedOptions(options));
    }

    /// <summary>
    /// 构造样例文档
    /// </summary>
    /// <param name="id">文档标识</param>
    /// <returns>文档</returns>
    private static SearchDocument<TestArticle> NewDocument(string id)
    {
        return new SearchDocument<TestArticle>(id, new TestArticle { Title = "标题" });
    }

    /// <summary>
    /// 固定返回同一实例的配置访问器
    /// </summary>
    /// <param name="value">配置</param>
    private sealed class FixedOptions(ElasticsearchOptions value) : IOptions<ElasticsearchOptions>
    {
        /// <summary>
        /// 配置
        /// </summary>
        public ElasticsearchOptions Value { get; } = value;
    }

    /// <summary>
    /// 测试用文档
    /// </summary>
    private sealed class TestArticle
    {
        /// <summary>
        /// 标题
        /// </summary>
        public string Title { get; set; } = string.Empty;
    }
}
