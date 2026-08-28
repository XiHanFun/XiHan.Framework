// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using XiHan.Framework.Web.Core.Clients;
using XiHan.Framework.Web.Core.Extensions.DependencyInjection;
using XiHan.Framework.Web.Core.Options;
using XiHan.Framework.Web.Core.Tests.Infrastructure;

namespace XiHan.Framework.Web.Core.Tests.Clients;

/// <summary>
/// 基于 HttpContext 的客户端信息提供器测试
/// </summary>
/// <remarks>
/// 这个提供器是审计日志里 IP、归属地、设备信息的唯一来源，取值口径出错会污染全部审计数据，
/// 因此重点覆盖三条：多级代理下取哪一跳、代理头缺失/全空白时的回退次序、异常 UA 不能把整次采集打挂。
/// 全部用例用 <see cref="DefaultHttpContext"/> 直接造请求上下文，不起真实管道。
/// 归属地部分刻意把内容根指向一个不存在的目录，让 ip2region 的三个候选路径统统落空，
/// 这样"公网 IP 的归属地"结果稳定为 null，用例不依赖仓库里是否放了 xdb 数据文件。
/// </remarks>
public class HttpContextClientInfoProviderTests
{
    /// <summary>
    /// 桌面版 Chrome 的典型 UA
    /// </summary>
    private const string DesktopChromeUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    /// <summary>
    /// 一个不含任何 ip2region 数据库的内容根目录
    /// </summary>
    private static readonly string ContentRootWithoutIpDatabase =
        Path.Combine(Path.GetTempPath(), "xihan-web-core-tests-no-ip2region-db");

    /// <summary>
    /// 没有 HttpContext 时（后台任务、宿主启动阶段）不得抛异常，六个字段全空
    /// </summary>
    [Fact]
    public void GetCurrent_WithoutHttpContext_ReturnsEmptyInfo()
    {
        using var provider = CreateProvider(null);

        var info = provider.GetCurrent();

        Assert.NotNull(info);
        Assert.Null(info.IpAddress);
        Assert.Null(info.Location);
        Assert.Null(info.UserAgent);
        Assert.Null(info.Browser);
        Assert.Null(info.OperatingSystem);
        Assert.Null(info.DeviceName);
    }

    /// <summary>
    /// 既无代理头也无连接地址时，IP 与归属地都是空，不能兜出空串
    /// </summary>
    [Fact]
    public void GetCurrent_WithoutAnyAddressSource_ReturnsNullIpAndLocation()
    {
        using var provider = CreateProvider(new DefaultHttpContext());

        var info = provider.GetCurrent();

        Assert.Null(info.IpAddress);
        Assert.Null(info.Location);
    }

    /// <summary>
    /// 多级代理时取 X-Forwarded-For 的第一跳，并对空白项、IPv4 映射地址做规范化
    /// </summary>
    /// <param name="headerValue">X-Forwarded-For 头原文</param>
    /// <param name="expected">期望解析出的 IP</param>
    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("203.0.113.7, 198.51.100.5, 70.41.3.18", "203.0.113.7")]
    [InlineData("  203.0.113.7  ,198.51.100.5", "203.0.113.7")]
    [InlineData(" , 203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    public void GetCurrent_WithForwardedFor_TakesFirstHop(string headerValue, string expected)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Forwarded-For"] = headerValue;
        httpContext.Request.Headers["X-Real-IP"] = "198.51.100.222";
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.9");

        using var provider = CreateProvider(httpContext);

        Assert.Equal(expected, provider.GetCurrent().IpAddress);
    }

    /// <summary>
    /// X-Forwarded-For 全空白时跳过它，退到 X-Real-IP
    /// </summary>
    /// <param name="forwardedFor">X-Forwarded-For 头原文</param>
    [Theory]
    [InlineData("   ")]
    [InlineData(" , , ")]
    public void GetCurrent_WhenForwardedForIsBlank_FallsBackToRealIp(string forwardedFor)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Forwarded-For"] = forwardedFor;
        httpContext.Request.Headers["X-Real-IP"] = " 198.51.100.9 ";
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.9");

        using var provider = CreateProvider(httpContext);

        Assert.Equal("198.51.100.9", provider.GetCurrent().IpAddress);
    }

    /// <summary>
    /// 两个代理头都缺失时退到连接的远端地址
    /// </summary>
    [Fact]
    public void GetCurrent_WithoutProxyHeaders_FallsBackToRemoteIpAddress()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.20");

        using var provider = CreateProvider(httpContext, new XiHanClientInfoOptions { EnableIpRegion = false });

        Assert.Equal("203.0.113.20", provider.GetCurrent().IpAddress);
    }

    /// <summary>
    /// 连接地址是 IPv4 映射的 IPv6 时折回成 IPv4 文本，避免同一客户端在审计里出现两种写法
    /// </summary>
    [Fact]
    public void GetCurrent_WhenRemoteAddressIsIPv4Mapped_NormalizesToIPv4()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:203.0.113.20");

        using var provider = CreateProvider(httpContext, new XiHanClientInfoOptions { EnableIpRegion = false });

        Assert.Equal("203.0.113.20", provider.GetCurrent().IpAddress);
    }

    /// <summary>
    /// 三个来源同时存在时的优先级：X-Forwarded-For 高于 X-Real-IP 高于连接地址
    /// </summary>
    [Fact]
    public void GetCurrent_WithAllAddressSources_PrefersForwardedFor()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Forwarded-For"] = "203.0.113.7";
        httpContext.Request.Headers["X-Real-IP"] = "198.51.100.9";
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("70.41.3.18");

        using var provider = CreateProvider(httpContext, new XiHanClientInfoOptions { EnableIpRegion = false });

        Assert.Equal("203.0.113.7", provider.GetCurrent().IpAddress);
    }

    /// <summary>
    /// X-Real-IP 高于连接地址
    /// </summary>
    [Fact]
    public void GetCurrent_WithRealIpAndRemote_PrefersRealIp()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Real-IP"] = "198.51.100.9";
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("70.41.3.18");

        using var provider = CreateProvider(httpContext, new XiHanClientInfoOptions { EnableIpRegion = false });

        Assert.Equal("198.51.100.9", provider.GetCurrent().IpAddress);
    }

    /// <summary>
    /// 环回与内网地址不查库，直接给出固定归属地文案
    /// </summary>
    /// <param name="ipAddress">客户端 IP</param>
    /// <param name="expectedLocation">期望归属地</param>
    [Theory]
    [InlineData("127.0.0.1", "本机")]
    [InlineData("127.5.6.7", "本机")]
    [InlineData("::1", "本机")]
    [InlineData("10.1.2.3", "局域网")]
    [InlineData("172.16.0.1", "局域网")]
    [InlineData("172.31.255.254", "局域网")]
    [InlineData("192.168.1.5", "局域网")]
    public void GetCurrent_ForReservedIp_ResolvesLocationWithoutDatabase(string ipAddress, string expectedLocation)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Forwarded-For"] = ipAddress;

        using var provider = CreateProvider(httpContext);

        Assert.Equal(expectedLocation, provider.GetCurrent().Location);
    }

    /// <summary>
    /// 内网段边界外的地址不得被误判成局域网
    /// </summary>
    /// <param name="ipAddress">客户端 IP</param>
    /// <remarks>
    /// 关掉 IP 地理库后公网地址的归属地必然为 null，
    /// 因此"结果是 null 而不是局域网"就足以证明 172.15/172.32 没有落进 172.16-172.31 的私网判定。
    /// </remarks>
    [Theory]
    [InlineData("172.15.0.1")]
    [InlineData("172.32.0.1")]
    [InlineData("191.168.1.5")]
    [InlineData("203.0.113.20")]
    public void GetCurrent_ForPublicIp_IsNotClassifiedAsPrivate(string ipAddress)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Forwarded-For"] = ipAddress;

        using var provider = CreateProvider(httpContext, new XiHanClientInfoOptions { EnableIpRegion = false });

        var info = provider.GetCurrent();

        Assert.Equal(ipAddress, info.IpAddress);
        Assert.Null(info.Location);
    }

    /// <summary>
    /// 开启地理解析但数据库文件缺失时安静降级为空归属地，不能抛异常打断请求
    /// </summary>
    [Fact]
    public void GetCurrent_WhenIpDatabaseMissing_DegradesToNullLocation()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Forwarded-For"] = "203.0.113.20";

        using var provider = CreateProvider(httpContext, new XiHanClientInfoOptions
        {
            EnableIpRegion = true,
            Ip2RegionDbPath = "IpDatabases/definitely-missing-ip2region.xdb"
        });

        var info = provider.GetCurrent();

        Assert.Equal("203.0.113.20", info.IpAddress);
        Assert.Null(info.Location);
    }

    /// <summary>
    /// 桌面浏览器 UA 能解析出浏览器与操作系统，原始 UA 原样保留
    /// </summary>
    [Fact]
    public void GetCurrent_WithDesktopUserAgent_ParsesBrowserAndOperatingSystem()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.UserAgent = DesktopChromeUserAgent;

        using var provider = CreateProvider(httpContext);

        var info = provider.GetCurrent();

        Assert.Equal(DesktopChromeUserAgent, info.UserAgent);
        Assert.NotNull(info.Browser);
        Assert.Contains("Chrome", info.Browser, StringComparison.Ordinal);
        Assert.NotNull(info.OperatingSystem);
        Assert.Contains("Windows", info.OperatingSystem, StringComparison.Ordinal);
        Assert.NotNull(info.DeviceName);
    }

    /// <summary>
    /// 无法识别的 UA 不抛异常：浏览器与系统留空，设备名回退为 PC
    /// </summary>
    /// <remarks>
    /// UAParser 对匹配不上的 UA 一律返回 Other，源码把 Other 的浏览器/系统归一为 null、把设备归一为 PC。
    /// 这条同时锁住"解析失败不能污染审计字段"和设备名的兜底文案。
    /// </remarks>
    [Fact]
    public void GetCurrent_WithUnrecognizableUserAgent_LeavesBrowserAndOsNullAndFallsBackToPc()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.UserAgent = "XiHanTestAgent";

        using var provider = CreateProvider(httpContext);

        var info = provider.GetCurrent();

        Assert.Equal("XiHanTestAgent", info.UserAgent);
        Assert.Null(info.Browser);
        Assert.Null(info.OperatingSystem);
        Assert.Equal("PC", info.DeviceName);
    }

    /// <summary>
    /// UA 头缺失或全空白时四个 UA 相关字段全空
    /// </summary>
    /// <param name="userAgent">UA 头原文</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GetCurrent_WithBlankUserAgent_ReturnsNullUserAgentFields(string userAgent)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.UserAgent = userAgent;

        using var provider = CreateProvider(httpContext);

        var info = provider.GetCurrent();

        Assert.Null(info.UserAgent);
        Assert.Null(info.Browser);
        Assert.Null(info.OperatingSystem);
        Assert.Null(info.DeviceName);
    }

    /// <summary>
    /// UA 头两端的空白会被裁掉后再入库
    /// </summary>
    [Fact]
    public void GetCurrent_WithPaddedUserAgent_TrimsIt()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.UserAgent = "   XiHanTestAgent   ";

        using var provider = CreateProvider(httpContext);

        Assert.Equal("XiHanTestAgent", provider.GetCurrent().UserAgent);
    }

    /// <summary>
    /// 提供器注册为单例，多线程并发采集必须给出一致结果且不抛异常
    /// </summary>
    /// <remarks>
    /// 并发点在于 IP 查询器的懒加载（双检锁），所以这里刻意开着地理解析走那条路径。
    /// </remarks>
    [Fact]
    public async Task GetCurrent_UnderConcurrency_StaysConsistent()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Forwarded-For"] = "203.0.113.30";
        httpContext.Request.Headers.UserAgent = "XiHanTestAgent";

        using var provider = CreateProvider(httpContext);

        var results = await Task.WhenAll(Enumerable.Range(0, 32)
            .Select(_ => Task.Run(provider.GetCurrent, TestContext.Current.CancellationToken)));

        Assert.Equal(32, results.Length);
        Assert.All(results, info =>
        {
            Assert.Equal("203.0.113.30", info.IpAddress);
            Assert.Null(info.Location);
            Assert.Equal("XiHanTestAgent", info.UserAgent);
            Assert.Equal("PC", info.DeviceName);
        });
    }

    /// <summary>
    /// 每次采集返回全新实例，调用方改写结果不会串到下一次
    /// </summary>
    [Fact]
    public void GetCurrent_CalledTwice_ReturnsIndependentInstances()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Forwarded-For"] = "203.0.113.30";

        using var provider = CreateProvider(httpContext, new XiHanClientInfoOptions { EnableIpRegion = false });

        var first = provider.GetCurrent();
        var second = provider.GetCurrent();

        Assert.NotSame(first, second);
        Assert.Equal(first.IpAddress, second.IpAddress);
    }

    /// <summary>
    /// 从未创建过查询器时释放不报错，且允许重复释放
    /// </summary>
    [Fact]
    public void Dispose_WithoutSearcher_IsSafeAndIdempotent()
    {
        var provider = CreateProvider(new DefaultHttpContext());

        var exception = Record.Exception(() =>
        {
            provider.Dispose();
            provider.Dispose();
        });

        Assert.Null(exception);
    }

    /// <summary>
    /// 构造一个提供器，内容根固定指向不存在 ip2region 数据库的目录
    /// </summary>
    /// <param name="httpContext">当前请求上下文，可为 null</param>
    /// <param name="options">客户端信息解析配置，默认取默认值</param>
    /// <returns>提供器实例</returns>
    private static HttpContextClientInfoProvider CreateProvider(HttpContext? httpContext, XiHanClientInfoOptions? options = null)
    {
        var hostingEnvironment = new EmptyHostingEnvironment
        {
            EnvironmentName = "Testing",
            ApplicationName = "XiHan.Framework.Web.Core.Tests",
            ContentRootPath = ContentRootWithoutIpDatabase,
            WebRootPath = ContentRootWithoutIpDatabase
        };

        return new HttpContextClientInfoProvider(
            new FakeHttpContextAccessor { HttpContext = httpContext },
            hostingEnvironment,
            new OptionsWrapper<XiHanClientInfoOptions>(options ?? new XiHanClientInfoOptions()),
            NullLogger<HttpContextClientInfoProvider>.Instance);
    }
}
