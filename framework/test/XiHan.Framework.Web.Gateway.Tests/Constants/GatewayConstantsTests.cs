// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Web.Gateway.Constants;

namespace XiHan.Framework.Web.Gateway.Tests.Constants;

/// <summary>
/// 网关常量测试
/// </summary>
/// <remarks>
/// 这批常量有两种对外契约身份：一是跨中间件传递数据用的 HttpContext.Items 键，
/// 二是直接出现在 HTTP 报文里的头名。两者一旦改动都会静默破坏调用方（业务代码、前端、链路追踪采集器），
/// 因此逐个锁死字面量，而不是只断言「非空」。
/// </remarks>
public class GatewayConstantsTests
{
    /// <summary>
    /// 上下文键字面量保持稳定
    /// </summary>
    [Fact]
    public void ContextKeys_AreStable()
    {
        Assert.Equal("XiHan.Gateway.TraceId", GatewayConstants.TraceIdKey);
        Assert.Equal("XiHan.Gateway.GrayDecision", GatewayConstants.GrayDecisionKey);
        Assert.Equal("XiHan.Gateway.RateLimit", GatewayConstants.RateLimitKey);
        Assert.Equal("XiHan.Gateway.CircuitBreaker", GatewayConstants.CircuitBreakerKey);
        Assert.Equal("XiHan.Gateway.RequestContext", GatewayConstants.RequestContextKey);
    }

    /// <summary>
    /// 对外 HTTP 头名保持稳定
    /// </summary>
    [Fact]
    public void Headers_AreStable()
    {
        Assert.Equal("X-Trace-Id", GatewayConstants.Headers.TraceId);
        Assert.Equal("X-Gray-Version", GatewayConstants.Headers.GrayVersion);
        Assert.Equal("X-User-Id", GatewayConstants.Headers.UserId);
        Assert.Equal("X-Tenant-Id", GatewayConstants.Headers.TenantId);
    }

    /// <summary>
    /// 上下文键互不重复
    /// </summary>
    /// <remarks>
    /// 键重复会让后写入的中间件覆盖前一个的数据，且不会有任何编译期或运行期报错。
    /// </remarks>
    [Fact]
    public void ContextKeys_AreDistinct()
    {
        string[] keys =
        [
            GatewayConstants.TraceIdKey,
            GatewayConstants.GrayDecisionKey,
            GatewayConstants.RateLimitKey,
            GatewayConstants.CircuitBreakerKey,
            GatewayConstants.RequestContextKey
        ];

        Assert.Equal(keys.Length, keys.Distinct().Count());
    }

    /// <summary>
    /// 上下文键统一使用 XiHan.Gateway 前缀
    /// </summary>
    /// <remarks>
    /// Items 是全进程共享的弱类型字典，没有前缀极易和其他模块的键撞车。
    /// </remarks>
    [Theory]
    [InlineData(GatewayConstants.TraceIdKey)]
    [InlineData(GatewayConstants.GrayDecisionKey)]
    [InlineData(GatewayConstants.RateLimitKey)]
    [InlineData(GatewayConstants.CircuitBreakerKey)]
    [InlineData(GatewayConstants.RequestContextKey)]
    public void ContextKeys_UseGatewayPrefix(string key)
    {
        Assert.StartsWith("XiHan.Gateway.", key);
    }
}
