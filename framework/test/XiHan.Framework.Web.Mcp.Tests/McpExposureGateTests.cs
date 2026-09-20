// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.AI.Mcp;
using XiHan.Framework.Web.Mcp.Extensions.DependencyInjection;
using XiHan.Framework.Web.Mcp.Options;

namespace XiHan.Framework.Web.Mcp.Tests;

/// <summary>
/// fail-closed 门控：未开启或未配密钥时，既不注册 MCP 服务，也不映射 /mcp 端点
/// </summary>
/// <remarks>
/// <see cref="XiHanMcpOptions.IsExposable"/> 同时被 <c>AddXiHanWebMcp</c> 与 <c>MapXiHanMcp</c> 引用，
/// 决定服务注册与端点映射是否发生。测试分别覆盖端点存在性与服务注册两个维度。
/// </remarks>
public class McpExposureGateTests
{
    /// <summary>
    /// 三种「没配全」的姿势，每一种都必须什么都不暴露
    /// </summary>
    public static TheoryData<bool, string?, string> 未就绪暴露的配置 =>
        new()
        {
            { false, "test-api-key", "配了密钥但没开启用" },
            { true, null, "开了启用但根本没有 ApiKey 这个键" },
            { true, "   ", "开了启用但 ApiKey 只有空白字符" }
        };

    /// <summary>
    /// 未就绪暴露时，/mcp 上没有任何端点，请求得到 404
    /// </summary>
    /// <param name="enabled">是否启用</param>
    /// <param name="apiKey">访问密钥</param>
    /// <param name="scenario">场景说明，失败时用来指认是哪一种配错</param>
    [Theory]
    [MemberData(nameof(未就绪暴露的配置))]
    public async Task 未就绪暴露时端点不存在(bool enabled, string? apiKey, string scenario)
    {
        await using var host = await McpTestHost.StartAsync(enabled, apiKey);

        var response = await host.PostInitializeAsync();

        // 404 之外，401 同样判定为失败：401 意味着端点已被映射
        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound,
            $"{scenario}：期望 404（端点根本不存在），实际是 {(int)response.StatusCode} {response.StatusCode}。");
    }

    /// <summary>
    /// 未就绪暴露时，容器里没有任何 MCP 服务
    /// </summary>
    /// <param name="enabled">是否启用</param>
    /// <param name="apiKey">访问密钥</param>
    /// <param name="scenario">场景说明</param>
    [Theory]
    [MemberData(nameof(未就绪暴露的配置))]
    public void 未就绪暴露时不注册任何MCP服务(bool enabled, string? apiKey, string scenario)
    {
        var services = BuildServices(enabled, apiKey);

        var mcpDescriptors = services.Where(IsMcpDescriptor).ToArray();

        Assert.True(
            mcpDescriptors.Length == 0,
            $"{scenario}：不该注册 MCP 服务，却注册了 {mcpDescriptors.Length} 项，"
            + $"例如 {string.Join("、", mcpDescriptors.Take(5).Select(descriptor => descriptor.ServiceType.Name))}。");
    }

    /// <summary>
    /// 未就绪暴露时选项照样绑上，「不注册服务」不等于「配置没读」
    /// </summary>
    /// <param name="enabled">是否启用</param>
    /// <param name="apiKey">访问密钥</param>
    /// <param name="scenario">场景说明</param>
    [Theory]
    [MemberData(nameof(未就绪暴露的配置))]
    public void 未就绪暴露时选项仍然完成绑定(bool enabled, string? apiKey, string scenario)
    {
        var services = BuildServices(enabled, apiKey);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XiHanMcpOptions>>().Value;

        Assert.Equal(enabled, options.Enabled);
        Assert.Equal(apiKey, options.ApiKey);
        Assert.False(options.IsExposable, $"{scenario}：这份配置不该被判定为可暴露。");
    }

    /// <summary>
    /// 反向对照：配全了就该既注册服务也映射端点
    /// </summary>
    [Fact]
    public async Task 就绪暴露时既注册MCP服务也映射端点()
    {
        const string ApiKey = "exposure-control-key";

        var services = BuildServices(enabled: true, ApiKey);
        var mcpDescriptors = services.Where(IsMcpDescriptor).ToArray();

        Assert.NotEmpty(mcpDescriptors);
        Assert.Contains(
            services,
            descriptor => descriptor.ImplementationType == typeof(SkillMcpToolsConfigurator));

        await using var host = await McpTestHost.StartAsync(enabled: true, ApiKey);

        var response = await host.PostInitializeAsync(
            request => request.Headers.TryAddWithoutValidation("X-Api-Key", ApiKey));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// 按给定配置跑一遍 <c>AddXiHanWebMcp</c>，返回装配后的服务集合
    /// </summary>
    /// <param name="enabled">是否启用</param>
    /// <param name="apiKey">访问密钥</param>
    /// <returns>装配后的服务集合</returns>
    private static ServiceCollection BuildServices(bool enabled, string? apiKey)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(McpTestHost.BuildSettings(enabled, apiKey))
            .Build();

        var services = new ServiceCollection();
        _ = services.AddXiHanWebMcp(configuration);

        return services;
    }

    /// <summary>
    /// 判断一个服务描述符是否由 MCP 装配引入
    /// </summary>
    /// <param name="descriptor">服务描述符</param>
    /// <returns>由 MCP 装配引入时为 true</returns>
    private static bool IsMcpDescriptor(ServiceDescriptor descriptor)
    {
        return IsMcpType(descriptor.ServiceType)
            || (descriptor.ImplementationType is { } implementationType && IsMcpType(implementationType))
            || (descriptor.ImplementationInstance?.GetType() is { } instanceType && IsMcpType(instanceType));
    }

    /// <summary>
    /// 判断一个类型是否属于 MCP 装配
    /// </summary>
    /// <remarks>
    /// 递归检查泛型实参，以覆盖 <c>IConfigureOptions&lt;McpServerOptions&gt;</c> 这类形态。
    /// </remarks>
    /// <param name="type">待判断的类型</param>
    /// <returns>属于 MCP 装配时为 true</returns>
    private static bool IsMcpType(Type type)
    {
        return type == typeof(SkillMcpToolsConfigurator)
            || type.Namespace?.StartsWith("ModelContextProtocol", StringComparison.Ordinal) == true
            || (type.IsGenericType && type.GetGenericArguments().Any(IsMcpType));
    }
}
