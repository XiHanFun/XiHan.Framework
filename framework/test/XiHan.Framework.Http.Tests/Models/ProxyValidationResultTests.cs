// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Http.Configuration;
using XiHan.Framework.Http.Enums;
using XiHan.Framework.Http.Models;

namespace XiHan.Framework.Http.Tests.Models;

/// <summary>
/// <see cref="ProxyValidationResult"/> 的构造与字段测试
/// </summary>
public class ProxyValidationResultTests
{
    /// <summary>
    /// 成功工厂设置代理、响应时间与可用标志
    /// </summary>
    [Fact]
    public void Success_SetsProxyResponseTimeAndAvailability()
    {
        var proxy = CreateProxy();

        var result = ProxyValidationResult.Success(proxy, 123L);

        Assert.True(result.IsAvailable);
        Assert.Same(proxy, result.Proxy);
        Assert.Equal(123L, result.ResponseTimeMilliseconds);
        Assert.Null(result.ErrorMessage);
    }

    /// <summary>
    /// 失败工厂设置代理、错误消息与不可用标志
    /// </summary>
    [Fact]
    public void Failure_SetsProxyErrorMessageAndUnavailability()
    {
        var proxy = CreateProxy();

        var result = ProxyValidationResult.Failure(proxy, "timeout");

        Assert.False(result.IsAvailable);
        Assert.Same(proxy, result.Proxy);
        Assert.Equal("timeout", result.ErrorMessage);
        Assert.Equal(0L, result.ResponseTimeMilliseconds);
    }

    /// <summary>
    /// 创建最小可用代理配置
    /// </summary>
    /// <returns>代理配置</returns>
    private static ProxyConfiguration CreateProxy() =>
        new() { Host = "127.0.0.1", Port = 8080, Type = ProxyType.Http };
}
