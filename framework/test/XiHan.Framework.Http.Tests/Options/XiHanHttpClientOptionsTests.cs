// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Http.Options;

namespace XiHan.Framework.Http.Tests.Options;

/// <summary>
/// <see cref="XiHanHttpClientOptions"/> 的默认值测试
/// </summary>
public class XiHanHttpClientOptionsTests
{
    /// <summary>
    /// 默认值符合预期且处于校验范围内
    /// </summary>
    [Fact]
    public void Defaults_AreReasonable()
    {
        var options = new XiHanHttpClientOptions();

        Assert.Equal(60, options.DefaultTimeoutSeconds);
        Assert.Equal(3, options.RetryCount);
        Assert.Equal([1, 5, 10], options.RetryDelaySeconds);
        Assert.Equal(5, options.CircuitBreakerFailureThreshold);
        Assert.Equal(60, options.CircuitBreakerSamplingDurationSeconds);
        Assert.Equal(10, options.CircuitBreakerMinimumThroughput);
        Assert.Equal(10, options.CircuitBreakerDurationOfBreakSeconds);
        Assert.True(options.EnableRequestLogging);
        Assert.True(options.EnableResponseLogging);
        Assert.False(options.LogSensitiveData);
        Assert.Equal(4096, options.MaxResponseContentLength);
        Assert.Equal(5, options.ClientLifetimeMinutes);
        Assert.False(options.IgnoreSslErrors);
        Assert.Empty(options.DefaultHeaders);
        Assert.Empty(options.Clients);
    }

    /// <summary>
    /// 配置节名称为 XiHan:Http
    /// </summary>
    [Fact]
    public void SectionName_IsXiHanHttp()
    {
        Assert.Equal("XiHan:Http", XiHanHttpClientOptions.SectionName);
    }
}
