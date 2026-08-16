// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using XiHan.Framework.Http.Configuration;
using XiHan.Framework.Http.Options;

namespace XiHan.Framework.Http.Tests.Options;

/// <summary>
/// <see cref="XiHanHttpRequestOptions"/> 的默认值与查询字符串构建测试
/// </summary>
public class XiHanHttpRequestOptionsTests
{
    /// <summary>
    /// 默认值符合预期
    /// </summary>
    [Fact]
    public void Defaults_AreReasonable()
    {
        var options = new XiHanHttpRequestOptions();

        Assert.Empty(options.Headers);
        Assert.Empty(options.QueryParameters);
        Assert.Null(options.Timeout);
        Assert.Null(options.EnableRetry);
        Assert.Null(options.EnableCircuitBreaker);
        Assert.Equal("application/json", options.ContentType);
        Assert.Same(Encoding.UTF8, options.Encoding);
        Assert.Null(options.ValidateSslCertificate);
        Assert.Null(options.RequestId);
        Assert.Null(options.Proxy);
        Assert.False(options.UseProxyPool);
    }

    /// <summary>
    /// 无查询参数时返回空字符串
    /// </summary>
    [Fact]
    public void BuildQueryString_ReturnsEmpty_WhenNoParameters()
    {
        var options = new XiHanHttpRequestOptions();

        Assert.Equal(string.Empty, options.BuildQueryString());
    }

    /// <summary>
    /// 查询字符串对键与值进行百分号编码
    /// </summary>
    [Fact]
    public void BuildQueryString_EncodesKeysAndValues()
    {
        var options = new XiHanHttpRequestOptions();
        options.AddQueryParameter("search term", "a&b=c");
        options.AddQueryParameter("中文", "值");

        var result = options.BuildQueryString();

        Assert.StartsWith("?", result);
        Assert.Contains("search%20term=a%26b%3Dc", result);
        Assert.Contains("%E4%B8%AD%E6%96%87=%E5%80%BC", result);
    }

    /// <summary>
    /// 链式配置方法返回同一实例
    /// </summary>
    [Fact]
    public void FluentMethods_ReturnSameInstance()
    {
        var proxy = new ProxyConfiguration { Host = "127.0.0.1", Port = 8080 };
        var options = new XiHanHttpRequestOptions();

        var returned = options
            .AddHeader("X-Test", "1")
            .AddQueryParameter("q", "1")
            .AddTag("key", "value")
            .SetTimeout(TimeSpan.FromSeconds(5))
            .SetRequestId("rid")
            .SetProxy(proxy)
            .EnableProxyPool();

        Assert.Same(options, returned);
        Assert.Equal("1", options.Headers["X-Test"]);
        Assert.Equal("1", options.QueryParameters["q"]);
        Assert.Equal("value", options.Tags["key"]);
        Assert.Equal(TimeSpan.FromSeconds(5), options.Timeout);
        Assert.Equal("rid", options.RequestId);
        Assert.Same(proxy, options.Proxy);
        Assert.True(options.UseProxyPool);
    }
}
