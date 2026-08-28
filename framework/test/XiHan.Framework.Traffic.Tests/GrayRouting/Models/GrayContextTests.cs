// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Traffic.GrayRouting.Models;

namespace XiHan.Framework.Traffic.Tests.GrayRouting.Models;

/// <summary>
/// 灰度上下文测试
/// </summary>
/// <remarks>
/// 上下文的核心契约有两条：构造函数必须给出可直接写入的空集合，
/// 以及默认 Headers 必须忽略大小写——HeaderGrayMatcher 的命中判定完全依赖后者。
/// </remarks>
public class GrayContextTests
{
    /// <summary>
    /// 构造函数初始化出可直接写入的空集合
    /// </summary>
    [Fact]
    public void Ctor_InitializesHeadersAndExtensionDataAsEmptyCollections()
    {
        var context = new GrayContext();

        Assert.NotNull(context.Headers);
        Assert.Empty(context.Headers);
        Assert.NotNull(context.ExtensionData);
        Assert.Empty(context.ExtensionData);
    }

    /// <summary>
    /// 默认请求头字典忽略大小写
    /// </summary>
    [Fact]
    public void Ctor_HeadersIgnoreCase()
    {
        var context = new GrayContext();
        context.Headers!["X-Gray"] = "true";

        Assert.True(context.Headers.ContainsKey("x-gray"));
        Assert.Equal("true", context.Headers["X-GRAY"]);
    }

    /// <summary>
    /// 标量属性默认全部为 null，表示「信息缺失」而非「零值」
    /// </summary>
    [Fact]
    public void Ctor_ScalarPropertiesDefaultToNull()
    {
        var context = new GrayContext();

        Assert.Null(context.UserId);
        Assert.Null(context.TenantId);
        Assert.Null(context.RequestPath);
        Assert.Null(context.RequestMethod);
        Assert.Null(context.ClientIpAddress);
    }

    /// <summary>
    /// 每个实例持有各自独立的集合，不会互相串数据
    /// </summary>
    [Fact]
    public void Ctor_CreatesIndependentCollectionsPerInstance()
    {
        var first = new GrayContext();
        var second = new GrayContext();

        first.Headers!["X-Gray"] = "true";
        first.ExtensionData!["weight"] = 30;

        Assert.Empty(second.Headers!);
        Assert.Empty(second.ExtensionData!);
    }

    /// <summary>
    /// 忽略大小写是默认构造函数给的，替换成区分大小写的字典后语义随之改变
    /// </summary>
    /// <remarks>
    /// 锁死这一点是为了提醒调用方：自己 new 字典塞进 Headers 时必须显式带 OrdinalIgnoreCase，
    /// 否则请求头灰度会在大小写不一致时静默不命中。
    /// </remarks>
    [Fact]
    public void Headers_WhenReplacedWithOrdinalDictionary_BecomesCaseSensitive()
    {
        var context = new GrayContext
        {
            Headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["X-Gray"] = "true" }
        };

        Assert.True(context.Headers.ContainsKey("X-Gray"));
        Assert.False(context.Headers.ContainsKey("x-gray"));
    }

    /// <summary>
    /// 集合属性允许被显式置空，用于表达「本次请求没有任何请求头」
    /// </summary>
    [Fact]
    public void Headers_CanBeSetToNull()
    {
        var context = new GrayContext { Headers = null, ExtensionData = null };

        Assert.Null(context.Headers);
        Assert.Null(context.ExtensionData);
    }

    /// <summary>
    /// 全部标量属性可读写
    /// </summary>
    [Fact]
    public void ScalarProperties_AreReadWrite()
    {
        var context = new GrayContext
        {
            UserId = 1001L,
            TenantId = 2002L,
            RequestPath = "/api/orders",
            RequestMethod = "POST",
            ClientIpAddress = "192.168.1.10"
        };

        Assert.Equal(1001L, context.UserId);
        Assert.Equal(2002L, context.TenantId);
        Assert.Equal("/api/orders", context.RequestPath);
        Assert.Equal("POST", context.RequestMethod);
        Assert.Equal("192.168.1.10", context.ClientIpAddress);
    }
}
