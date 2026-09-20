// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.AI.Mcp;
using XiHan.Framework.Web.Mcp.Extensions.DependencyInjection;

namespace XiHan.Framework.Web.Mcp.Tests.Extensions;

/// <summary>
/// fail-closed 门控在真实宿主上的端到端覆盖
/// </summary>
/// <remarks>
/// 服务集合与选项绑定两个维度已由 <c>XiHanWebMcpServiceCollectionExtensionsTests</c> 覆盖，
/// 未就绪时不注册端点数据源也已由 <see cref="ApplicationBuilderExtensionsTests"/> 覆盖。
/// 这里只留它们证明不了的那部分：真实宿主上 /mcp 到底能不能被请求到。
/// 就绪分支同时断言服务注册，使 <c>IsMcpType</c> 判定不至于恒真通过。
/// </remarks>
public class McpExposureGateEndToEndTests
{
    /// <summary>
    /// 三种「没配全」的姿势，每一种都必须什么都不暴露
    /// </summary>
    public static TheoryData<bool, string?, string> NotExposableConfigurations =>
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
    [MemberData(nameof(NotExposableConfigurations))]
    public async Task MapXiHanMcp_WhenNotExposable_ExposesNoEndpoint(bool enabled, string? apiKey, string scenario)
    {
        await using var host = await McpTestHost.StartAsync(enabled, apiKey);

        var response = await host.PostInitializeAsync();

        // 404 之外，401 同样判定为失败：401 意味着端点已被映射
        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound,
            $"{scenario}：期望 404（端点根本不存在），实际是 {(int)response.StatusCode} {response.StatusCode}。");
    }

    /// <summary>
    /// 反向对照：配全了就该既注册服务也映射端点
    /// </summary>
    [Fact]
    public async Task MapXiHanMcp_WhenExposable_RegistersMcpServicesAndExposesEndpoint()
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
