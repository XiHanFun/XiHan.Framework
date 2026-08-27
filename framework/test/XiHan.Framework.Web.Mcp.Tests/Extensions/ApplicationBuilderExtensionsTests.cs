// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Web.Mcp.Extensions;
using XiHan.Framework.Web.Mcp.Options;

namespace XiHan.Framework.Web.Mcp.Tests.Extensions;

/// <summary>
/// MCP 端点映射扩展测试
/// </summary>
/// <remarks>
/// 这里只锁 fail-closed 那一半：配置未就绪时 <c>MapXiHanMcp</c> 必须一个端点数据源都不注册，
/// 否则 /mcp 会以未配密钥的形态挂到路由表上。就绪分支要真正落到 <c>MapMcp</c>，
/// 依赖 ModelContextProtocol 包在容器里的完整装配，属集成范畴，不在本工程覆盖。
/// </remarks>
public class ApplicationBuilderExtensionsTests
{
    /// <summary>
    /// 端点路由构建器为 null 时抛出参数异常
    /// </summary>
    [Fact]
    public void MapXiHanMcp_WithNullEndpoints_Throws()
    {
        IEndpointRouteBuilder endpoints = null!;

        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            _ = endpoints.MapXiHanMcp(new XiHanMcpOptions());
        });

        Assert.Equal("endpoints", exception.ParamName);
    }

    /// <summary>
    /// 配置为 null 时抛出参数异常
    /// </summary>
    [Fact]
    public void MapXiHanMcp_WithNullOptions_Throws()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var endpoints = new FakeEndpointRouteBuilder(provider);

        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            _ = endpoints.MapXiHanMcp(null!);
        });

        Assert.Equal("options", exception.ParamName);
    }

    /// <summary>
    /// 配置未就绪时不注册任何端点数据源
    /// </summary>
    /// <param name="enabled">是否启用</param>
    /// <param name="apiKey">配置的密钥</param>
    [Theory]
    [InlineData(false, null)]
    [InlineData(false, "valid-key")]
    [InlineData(true, null)]
    [InlineData(true, "")]
    [InlineData(true, "   ")]
    public void MapXiHanMcp_WhenNotExposable_RegistersNoEndpointSource(bool enabled, string? apiKey)
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var endpoints = new FakeEndpointRouteBuilder(provider);
        var options = new XiHanMcpOptions { Enabled = enabled, ApiKey = apiKey };

        _ = endpoints.MapXiHanMcp(options);

        Assert.False(options.IsExposable);
        Assert.Empty(endpoints.DataSources);
    }

    /// <summary>
    /// 未就绪时原样返回传入的端点路由构建器，便于链式调用
    /// </summary>
    [Fact]
    public void MapXiHanMcp_WhenNotExposable_ReturnsSameBuilder()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var endpoints = new FakeEndpointRouteBuilder(provider);

        var returned = endpoints.MapXiHanMcp(new XiHanMcpOptions());

        Assert.Same(endpoints, returned);
    }

    /// <summary>
    /// 未就绪时连路径配置都不会被读取，路径写错也不会炸在映射阶段
    /// </summary>
    [Fact]
    public void MapXiHanMcp_WhenNotExposable_IgnoresPathSetting()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var endpoints = new FakeEndpointRouteBuilder(provider);
        var options = new XiHanMcpOptions { Enabled = false, ApiKey = "valid-key", Path = "not-a-valid-route" };

        _ = endpoints.MapXiHanMcp(options);

        Assert.Empty(endpoints.DataSources);
    }

    /// <summary>
    /// 只记录端点数据源的手写端点路由构建器
    /// </summary>
    private sealed class FakeEndpointRouteBuilder : IEndpointRouteBuilder
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="serviceProvider">服务提供者</param>
        public FakeEndpointRouteBuilder(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        /// <summary>
        /// 服务提供者
        /// </summary>
        public IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// 已注册的端点数据源
        /// </summary>
        public ICollection<EndpointDataSource> DataSources { get; } = new List<EndpointDataSource>();

        /// <summary>
        /// 创建应用程序构建器
        /// </summary>
        /// <returns>应用程序构建器</returns>
        public IApplicationBuilder CreateApplicationBuilder()
        {
            return new ApplicationBuilder(ServiceProvider);
        }
    }
}
