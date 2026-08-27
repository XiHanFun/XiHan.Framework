// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using XiHan.Framework.Web.Mcp.Filters;

namespace XiHan.Framework.Web.Mcp.Tests.Filters;

/// <summary>
/// MCP 端点 API Key 过滤器测试
/// </summary>
/// <remarks>
/// /mcp 端点被显式标记 <c>AllowAnonymous()</c> 绕过了框架全局 FallbackPolicy，
/// 这个过滤器是该端点唯一的守门人：判定一旦放宽，整套 MCP 工具集就是裸奔状态。
/// 因此这里按「放行 / 拒绝」两侧穷举鉴权矩阵：自定义头、Bearer 回落、大小写、长度、
/// 重复头值、空密钥配置，并且每条拒绝用例都额外断言后续处理器一次都没有被调用
/// （SSE 流一旦开启就无法再收回，短路必须发生在 <c>next</c> 之前）。
/// 恒定时间比较本身无法用断言证明，这里只锁住它的可观测语义：逐字节严格相等、
/// 长度不同时不抛异常而是判否。
/// </remarks>
public class McpApiKeyEndpointFilterTests
{
    private const string ApiKey = "s3cr3t-Key-2024";
    private const string DefaultHeaderName = "X-Api-Key";

    private static readonly object NextResult = new();

    /// <summary>
    /// 过滤器实现端点过滤器接口，才能被 AddEndpointFilter 接受
    /// </summary>
    [Fact]
    public void Filter_ImplementsEndpointFilterContract()
    {
        var filter = new McpApiKeyEndpointFilter(ApiKey, DefaultHeaderName);

        Assert.IsAssignableFrom<IEndpointFilter>(filter);
    }

    /// <summary>
    /// 携带正确密钥的请求放行，并原样返回后续处理器的结果
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithMatchingHeaderKey_InvokesNextAndReturnsItsResult()
    {
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers[DefaultHeaderName] = ApiKey;

        var outcome = await RunAsync(CreateFilter(), httpContext);

        Assert.Equal(1, outcome.NextInvocationCount);
        Assert.Same(NextResult, outcome.Result);
    }

    /// <summary>
    /// 请求头名按 HTTP 语义大小写不敏感，客户端用小写头名同样放行
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithDifferentlyCasedHeaderName_InvokesNext()
    {
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers["x-api-key"] = ApiKey;

        var outcome = await RunAsync(CreateFilter(), httpContext);

        Assert.Equal(1, outcome.NextInvocationCount);
        Assert.Same(NextResult, outcome.Result);
    }

    /// <summary>
    /// 放行时把原始调用上下文原样传给后续处理器，不做包装或替换
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenAuthorized_PassesOriginalContextToNext()
    {
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers[DefaultHeaderName] = ApiKey;

        var outcome = await RunAsync(CreateFilter(), httpContext);

        Assert.Same(outcome.GivenContext, outcome.ContextPassedToNext);
    }

    /// <summary>
    /// 后续处理器返回 null 时过滤器照原样返回 null，不擅自替换成其它结果
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenNextReturnsNull_ReturnsNull()
    {
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers[DefaultHeaderName] = ApiKey;

        var outcome = await RunAsync(CreateFilter(), httpContext, nextResult: null);

        Assert.Equal(1, outcome.NextInvocationCount);
        Assert.Null(outcome.Result);
    }

    /// <summary>
    /// 不带任何凭据的请求直接 401，且不触碰后续处理器
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithoutAnyCredential_ReturnsUnauthorizedAndSkipsNext()
    {
        var outcome = await RunAsync(CreateFilter(), CreateHttpContext());

        AssertUnauthorized(outcome);
    }

    /// <summary>
    /// 请求头存在但值为空，视同未携带凭据
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithEmptyHeaderValue_ReturnsUnauthorized()
    {
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers[DefaultHeaderName] = string.Empty;

        var outcome = await RunAsync(CreateFilter(), httpContext);

        AssertUnauthorized(outcome);
    }

    /// <summary>
    /// 等长但内容不同的密钥被拒绝
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithWrongKeyOfSameLength_ReturnsUnauthorized()
    {
        var wrongKey = new string('x', ApiKey.Length);
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers[DefaultHeaderName] = wrongKey;

        var outcome = await RunAsync(CreateFilter(), httpContext);

        Assert.Equal(ApiKey.Length, wrongKey.Length);
        AssertUnauthorized(outcome);
    }

    /// <summary>
    /// 长度不同的密钥被判否而不是抛异常（恒定时间比较对长度不等直接返回 false）
    /// </summary>
    /// <param name="providedKey">客户端提供的密钥</param>
    [Theory]
    [InlineData("s3cr3t-Key-202")]
    [InlineData("s3cr3t-Key-20244")]
    [InlineData("s")]
    public async Task InvokeAsync_WithWrongKeyOfDifferentLength_ReturnsUnauthorized(string providedKey)
    {
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers[DefaultHeaderName] = providedKey;

        var outcome = await RunAsync(CreateFilter(), httpContext);

        AssertUnauthorized(outcome);
    }

    /// <summary>
    /// 密钥比较大小写敏感，仅大小写不同的密钥同样被拒绝
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithKeyDifferingOnlyInCase_ReturnsUnauthorized()
    {
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers[DefaultHeaderName] = ApiKey.ToUpperInvariant();

        var outcome = await RunAsync(CreateFilter(), httpContext);

        Assert.NotEqual(ApiKey, ApiKey.ToUpperInvariant());
        AssertUnauthorized(outcome);
    }

    /// <summary>
    /// 自定义头里的密钥不做首尾空白裁剪，带空白的值被拒绝
    /// </summary>
    /// <remarks>
    /// 与 Bearer 分支的 <c>Trim()</c> 是刻意的不对称：真实 HTTP 服务器会剥掉头值两侧的
    /// 可选空白，能走到这里的空白只可能是客户端刻意构造的，判否符合 fail-closed。
    /// </remarks>
    [Fact]
    public async Task InvokeAsync_WithHeaderKeyPaddedByWhitespace_ReturnsUnauthorized()
    {
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers[DefaultHeaderName] = " " + ApiKey + " ";

        var outcome = await RunAsync(CreateFilter(), httpContext);

        AssertUnauthorized(outcome);
    }

    /// <summary>
    /// 同名请求头被重复提交时（值会被合并成逗号分隔串）判否，避免注入式绕过
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithDuplicatedHeaderValues_ReturnsUnauthorized()
    {
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers[DefaultHeaderName] = new StringValues(new[] { ApiKey, "another" });

        var outcome = await RunAsync(CreateFilter(), httpContext);

        AssertUnauthorized(outcome);
    }

    /// <summary>
    /// 通过 Authorization: Bearer 携带正确密钥同样放行
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithBearerAuthorization_InvokesNext()
    {
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers.Authorization = "Bearer " + ApiKey;

        var outcome = await RunAsync(CreateFilter(), httpContext);

        Assert.Equal(1, outcome.NextInvocationCount);
        Assert.Same(NextResult, outcome.Result);
    }

    /// <summary>
    /// Bearer 方案名按 RFC 语义大小写不敏感
    /// </summary>
    /// <param name="scheme">方案名写法</param>
    [Theory]
    [InlineData("Bearer")]
    [InlineData("bearer")]
    [InlineData("BEARER")]
    [InlineData("BeArEr")]
    public async Task InvokeAsync_WithAnyCasedBearerScheme_InvokesNext(string scheme)
    {
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers.Authorization = scheme + " " + ApiKey;

        var outcome = await RunAsync(CreateFilter(), httpContext);

        Assert.Equal(1, outcome.NextInvocationCount);
    }

    /// <summary>
    /// Bearer 令牌两侧的空白被裁剪后仍能匹配
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithBearerTokenSurroundedByWhitespace_InvokesNext()
    {
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers.Authorization = "Bearer   " + ApiKey + "  ";

        var outcome = await RunAsync(CreateFilter(), httpContext);

        Assert.Equal(1, outcome.NextInvocationCount);
    }

    /// <summary>
    /// 非 Bearer 方案的 Authorization 头不被接受
    /// </summary>
    /// <param name="authorization">Authorization 头原文</param>
    [Theory]
    [InlineData("Basic czNjcjN0LUtleS0yMDI0")]
    [InlineData("Token s3cr3t-Key-2024")]
    [InlineData("s3cr3t-Key-2024")]
    [InlineData("Bearers3cr3t-Key-2024")]
    public async Task InvokeAsync_WithNonBearerAuthorization_ReturnsUnauthorized(string authorization)
    {
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers.Authorization = authorization;

        var outcome = await RunAsync(CreateFilter(), httpContext);

        AssertUnauthorized(outcome);
    }

    /// <summary>
    /// Bearer 里携带错误密钥被拒绝
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithBearerCarryingWrongKey_ReturnsUnauthorized()
    {
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers.Authorization = "Bearer wrong-key";

        var outcome = await RunAsync(CreateFilter(), httpContext);

        AssertUnauthorized(outcome);
    }

    /// <summary>
    /// 只有自定义头为空时才回落到 Authorization 分支
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithEmptyCustomHeaderAndValidBearer_InvokesNext()
    {
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers[DefaultHeaderName] = string.Empty;
        httpContext.Request.Headers.Authorization = "Bearer " + ApiKey;

        var outcome = await RunAsync(CreateFilter(), httpContext);

        Assert.Equal(1, outcome.NextInvocationCount);
    }

    /// <summary>
    /// 自定义头一旦有值就以它为准，不再回落到 Authorization
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithWrongCustomHeaderAndValidBearer_ReturnsUnauthorized()
    {
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers[DefaultHeaderName] = "wrong-key";
        httpContext.Request.Headers.Authorization = "Bearer " + ApiKey;

        var outcome = await RunAsync(CreateFilter(), httpContext);

        AssertUnauthorized(outcome);
    }

    /// <summary>
    /// 只读取构造时配置的头名，默认头名不再具有特权
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithCustomHeaderName_ReadsOnlyConfiguredHeader()
    {
        var filter = new McpApiKeyEndpointFilter(ApiKey, "X-Mcp-Key");

        var wrongHeaderContext = CreateHttpContext();
        wrongHeaderContext.Request.Headers[DefaultHeaderName] = ApiKey;
        AssertUnauthorized(await RunAsync(filter, wrongHeaderContext));

        var configuredHeaderContext = CreateHttpContext();
        configuredHeaderContext.Request.Headers["X-Mcp-Key"] = ApiKey;
        var allowed = await RunAsync(filter, configuredHeaderContext);

        Assert.Equal(1, allowed.NextInvocationCount);
    }

    /// <summary>
    /// 配置成空密钥时任何请求都进不去，包括同样送空值的请求
    /// </summary>
    /// <remarks>
    /// 选项层的 <c>IsExposable</c> 本就不会让空密钥暴露端点，这里再锁一道：
    /// 即便有人绕过选项直接构造过滤器，空密钥也必须是 fail-closed 而不是 fail-open。
    /// </remarks>
    /// <param name="providedKey">客户端提供的密钥，null 表示不带任何头</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("anything")]
    public async Task InvokeAsync_WithEmptyConfiguredKey_RejectsEveryRequest(string? providedKey)
    {
        var filter = new McpApiKeyEndpointFilter(string.Empty, DefaultHeaderName);
        var httpContext = CreateHttpContext();
        if (providedKey is not null)
        {
            httpContext.Request.Headers[DefaultHeaderName] = providedKey;
        }

        var outcome = await RunAsync(filter, httpContext);

        AssertUnauthorized(outcome);
    }

    /// <summary>
    /// 非 ASCII 密钥按 UTF-8 字节比较，等值放行、形近值拒绝
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithNonAsciiKey_ComparesUtf8Bytes()
    {
        const string NonAsciiKey = "密钥-π-Ω";
        var filter = new McpApiKeyEndpointFilter(NonAsciiKey, DefaultHeaderName);

        var matching = CreateHttpContext();
        matching.Request.Headers[DefaultHeaderName] = NonAsciiKey;
        Assert.Equal(1, (await RunAsync(filter, matching)).NextInvocationCount);

        var lookalike = CreateHttpContext();
        lookalike.Request.Headers[DefaultHeaderName] = "密钥-π-O";
        AssertUnauthorized(await RunAsync(filter, lookalike));
    }

    /// <summary>
    /// 拒绝时过滤器自己不写响应，返回的结果被执行后才落成 401
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenRejected_ReturnsResultThatExecutesAs401()
    {
        await using var provider = new ServiceCollection().AddLogging().BuildServiceProvider();
        var httpContext = CreateHttpContext();
        httpContext.RequestServices = provider;

        var outcome = await RunAsync(CreateFilter(), httpContext);

        // 端点过滤器只负责返回结果，写响应是结果执行阶段的事，短路时状态码还没落地
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);

        var result = Assert.IsAssignableFrom<IResult>(outcome.Result);
        await result.ExecuteAsync(httpContext);

        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
    }

    /// <summary>
    /// 断言这次调用被拒绝：返回 401 结果且后续处理器一次都没被调用
    /// </summary>
    /// <param name="outcome">过滤器执行结果</param>
    private static void AssertUnauthorized(FilterOutcome outcome)
    {
        Assert.Equal(0, outcome.NextInvocationCount);

        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(outcome.Result);

        Assert.NotNull(statusCodeResult.StatusCode);
        Assert.Equal(StatusCodes.Status401Unauthorized, statusCodeResult.StatusCode.Value);
    }

    /// <summary>
    /// 构造默认配置的过滤器
    /// </summary>
    /// <returns>过滤器</returns>
    private static McpApiKeyEndpointFilter CreateFilter()
    {
        return new McpApiKeyEndpointFilter(ApiKey, DefaultHeaderName);
    }

    /// <summary>
    /// 构造一个空白请求上下文
    /// </summary>
    /// <returns>请求上下文</returns>
    private static DefaultHttpContext CreateHttpContext()
    {
        return new DefaultHttpContext();
    }

    /// <summary>
    /// 驱动一次过滤器调用
    /// </summary>
    /// <param name="filter">被测过滤器</param>
    /// <param name="httpContext">请求上下文</param>
    /// <param name="nextResult">后续处理器的返回值</param>
    /// <returns>过滤器执行结果</returns>
    private static async Task<FilterOutcome> RunAsync(
        McpApiKeyEndpointFilter filter,
        HttpContext httpContext,
        object? nextResult)
    {
        var next = new NextInvocationTracker(nextResult);
        var invocationContext = EndpointFilterInvocationContext.Create(httpContext);

        var result = await filter.InvokeAsync(invocationContext, next.InvokeAsync);

        return new FilterOutcome(result, next.InvocationCount, next.LastContext, invocationContext);
    }

    /// <summary>
    /// 驱动一次过滤器调用，后续处理器返回固定哨兵值
    /// </summary>
    /// <param name="filter">被测过滤器</param>
    /// <param name="httpContext">请求上下文</param>
    /// <returns>过滤器执行结果</returns>
    private static Task<FilterOutcome> RunAsync(McpApiKeyEndpointFilter filter, HttpContext httpContext)
    {
        return RunAsync(filter, httpContext, NextResult);
    }

    /// <summary>
    /// 一次过滤器执行的观测结果
    /// </summary>
    /// <param name="Result">过滤器返回值</param>
    /// <param name="NextInvocationCount">后续处理器被调用的次数</param>
    /// <param name="ContextPassedToNext">传给后续处理器的调用上下文</param>
    /// <param name="GivenContext">交给过滤器的调用上下文</param>
    private sealed record FilterOutcome(
        object? Result,
        int NextInvocationCount,
        EndpointFilterInvocationContext? ContextPassedToNext,
        EndpointFilterInvocationContext GivenContext);

    /// <summary>
    /// 记录后续处理器调用情况的手写替身
    /// </summary>
    private sealed class NextInvocationTracker
    {
        private readonly object? _returnValue;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="returnValue">被调用时返回的值</param>
        public NextInvocationTracker(object? returnValue)
        {
            _returnValue = returnValue;
        }

        /// <summary>
        /// 被调用次数
        /// </summary>
        public int InvocationCount { get; private set; }

        /// <summary>
        /// 最后一次收到的调用上下文
        /// </summary>
        public EndpointFilterInvocationContext? LastContext { get; private set; }

        /// <summary>
        /// 充当端点过滤器管道里的后续委托
        /// </summary>
        /// <param name="context">调用上下文</param>
        /// <returns>固定返回值</returns>
        public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context)
        {
            InvocationCount++;
            LastContext = context;

            return ValueTask.FromResult<object?>(_returnValue);
        }
    }
}
