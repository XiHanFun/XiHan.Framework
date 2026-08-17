// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using XiHan.Framework.Docs.Mcp.Tools;
using XiHan.Framework.Docs.Mcp.Web.Extensions.DependencyInjection;

namespace XiHan.Framework.Docs.Mcp.Web.Tests;

/// <summary>
/// 服务注册层的 fail-closed 断言
/// </summary>
/// <remarks>
/// 「端点 404」只证明没映射端点，证明不了没注册服务；而 fail-closed 的意义正在于半配好的部署
/// 连 MCP 服务都不该建起来。所以这一组直接查 <see cref="IServiceCollection"/> 里有没有 MCP 相关登记，
/// 与 <see cref="EndpointExposureTests"/> 的 404 断言互补。
/// </remarks>
public class ServiceRegistrationTests
{
    /// <summary>
    /// 三种「只配了一半」的组合，逐条都必须不暴露
    /// </summary>
    public static TheoryData<string, bool, string?> 未就绪的三种配法 => new()
    {
        { "开关关闭但配了密钥", false, "secret-key" },
        { "开关打开但没有密钥", true, null },
        { "开关打开但密钥全是空白", true, "   " }
    };

    [Theory]
    [MemberData(nameof(未就绪的三种配法))]
    public void 未就绪暴露时不注册任何Mcp服务(string 场景, bool enabled, string? apiKey)
    {
        var services = BuildServices(enabled, apiKey);

        var descriptors = services
            .Where(d => d.ServiceType.FullName?.Contains("ModelContextProtocol", StringComparison.Ordinal) == true)
            .ToList();

        Assert.True(
            descriptors.Count == 0,
            $"{场景}：本应一个 MCP 服务都不注册，实际注册了 {descriptors.Count} 个（{string.Join("、", descriptors.Select(d => d.ServiceType.FullName))}）。");
    }

    [Theory]
    [MemberData(nameof(未就绪的三种配法))]
    public void 未就绪暴露时检索三层照常注册(string 场景, bool enabled, string? apiKey)
    {
        var services = BuildServices(enabled, apiKey);

        // 索引与工具本身与「是否对外暴露」无关，不该被 fail-closed 一并砍掉
        Assert.True(
            services.Any(d => d.ServiceType == typeof(DocsMcpTools)),
            $"{场景}：DocsMcpTools 应照常注册。");
    }

    [Fact]
    public void 就绪暴露时注册Mcp服务()
    {
        var services = BuildServices(enabled: true, apiKey: "secret-key");

        Assert.Contains(
            services,
            d => d.ServiceType.FullName?.Contains("ModelContextProtocol", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void 就绪暴露时三个文档工具全部登记()
    {
        var services = BuildServices(enabled: true, apiKey: "secret-key");

        var tools = services.Count(d => d.ServiceType == typeof(McpServerTool));

        Assert.Equal(3, tools);
    }

    /// <summary>
    /// 按给定开关与密钥装配一份服务集合
    /// </summary>
    private static IServiceCollection BuildServices(bool enabled, string? apiKey)
    {
        var settings = new List<KeyValuePair<string, string?>>
        {
            new("XiHan:Docs:Mcp:Enabled", enabled ? "true" : "false")
        };

        if (apiKey is not null)
        {
            settings.Add(new KeyValuePair<string, string?>("XiHan:Docs:Mcp:ApiKey", apiKey));
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddXiHanDocsMcpWeb(configuration, DocsMcpWebTestHost.RepositoryRoot);

        return services;
    }
}
