// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Web.Mcp.Extensions.DependencyInjection;
using XiHan.Framework.Web.Mcp.Options;

namespace XiHan.Framework.Web.Mcp.Tests.Extensions.DependencyInjection;

/// <summary>
/// 曦寒 Web MCP 服务注册扩展测试
/// </summary>
/// <remarks>
/// 注册端与映射端共用同一个 <see cref="XiHanMcpOptions.IsExposable"/> 判定，两边必须同进同退：
/// 未就绪时连 MCP 传输与技能工具都不该进容器（进了就意味着进程里躺着一份随时可被别处映射出去的
/// MCP server 装配）；就绪时两者都必须在。断言按「服务类型是否来自 ModelContextProtocol 程序集」
/// 判定，避免把第三方包的具体注册形态写死进测试。
/// 选项绑定则不受门控影响，任何情况下都要能解析出来，否则模块初始化阶段读配置会直接抛。
/// </remarks>
public class XiHanWebMcpServiceCollectionExtensionsTests
{
    /// <summary>
    /// 服务集合为 null 时抛出参数异常
    /// </summary>
    [Fact]
    public void AddXiHanWebMcp_WithNullServices_Throws()
    {
        IServiceCollection services = null!;
        var configuration = BuildConfiguration(enabled: true, apiKey: "valid-key");

        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            _ = services.AddXiHanWebMcp(configuration);
        });

        Assert.Equal("services", exception.ParamName);
    }

    /// <summary>
    /// 配置为 null 时抛出参数异常
    /// </summary>
    [Fact]
    public void AddXiHanWebMcp_WithNullConfiguration_Throws()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            _ = services.AddXiHanWebMcp(null!);
        });

        Assert.Equal("configuration", exception.ParamName);
    }

    /// <summary>
    /// 原样返回传入的服务集合，便于链式调用
    /// </summary>
    [Fact]
    public void AddXiHanWebMcp_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var returned = services.AddXiHanWebMcp(BuildConfiguration(enabled: false, apiKey: null));

        Assert.Same(services, returned);
    }

    /// <summary>
    /// 无论是否就绪都注册选项绑定
    /// </summary>
    /// <param name="enabled">是否启用</param>
    /// <param name="apiKey">配置的密钥</param>
    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(true, "valid-key")]
    public void AddXiHanWebMcp_AlwaysRegistersOptionsBinding(bool enabled, string? apiKey)
    {
        var services = new ServiceCollection();

        services.AddXiHanWebMcp(BuildConfiguration(enabled, apiKey));

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IConfigureOptions<XiHanMcpOptions>));
    }

    /// <summary>
    /// 未启用时只绑定选项，不注册任何 MCP 协议服务
    /// </summary>
    [Fact]
    public void AddXiHanWebMcp_WhenDisabled_RegistersNoMcpService()
    {
        var services = new ServiceCollection();

        services.AddXiHanWebMcp(BuildConfiguration(enabled: false, apiKey: "valid-key"));

        AssertNoMcpServiceRegistered(services);
    }

    /// <summary>
    /// 启用但没配密钥时同样不注册任何 MCP 协议服务
    /// </summary>
    /// <param name="apiKey">配置的密钥</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddXiHanWebMcp_WhenApiKeyMissing_RegistersNoMcpService(string? apiKey)
    {
        var services = new ServiceCollection();

        services.AddXiHanWebMcp(BuildConfiguration(enabled: true, apiKey: apiKey));

        AssertNoMcpServiceRegistered(services);
    }

    /// <summary>
    /// 配置里完全没有 MCP 节时按默认值走 fail-closed，不注册任何 MCP 协议服务
    /// </summary>
    [Fact]
    public void AddXiHanWebMcp_WithAbsentSection_RegistersNoMcpService()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddXiHanWebMcp(configuration);

        AssertNoMcpServiceRegistered(services);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XiHanMcpOptions>>().Value;

        Assert.False(options.IsExposable);
        Assert.Equal("X-Api-Key", options.HeaderName);
        Assert.Equal("/mcp", options.Path);
    }

    /// <summary>
    /// 未就绪时选项依然按配置绑定，供后续模块初始化读取
    /// </summary>
    [Fact]
    public void AddXiHanWebMcp_WhenDisabled_StillResolvesBoundOptions()
    {
        var services = new ServiceCollection();

        services.AddXiHanWebMcp(BuildConfiguration(enabled: false, apiKey: "valid-key", headerName: "X-Mcp-Key", path: "/internal/mcp"));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XiHanMcpOptions>>().Value;

        Assert.False(options.Enabled);
        Assert.Equal("valid-key", options.ApiKey);
        Assert.Equal("X-Mcp-Key", options.HeaderName);
        Assert.Equal("/internal/mcp", options.Path);
        Assert.False(options.IsExposable);
    }

    /// <summary>
    /// 就绪时注册 MCP 协议服务
    /// </summary>
    [Fact]
    public void AddXiHanWebMcp_WhenExposable_RegistersMcpProtocolServices()
    {
        var services = new ServiceCollection();

        services.AddXiHanWebMcp(BuildConfiguration(enabled: true, apiKey: "valid-key"));

        Assert.Contains(services, descriptor => DependsOnMcpProtocolTypes(descriptor));
    }

    /// <summary>
    /// 就绪时把技能注册表投影为 MCP 工具的配置器接进容器
    /// </summary>
    /// <remarks>
    /// 按实现类型全名断言而不是 <c>typeof</c>，是为了让本工程不必直接依赖 MCP 协议包的类型，
    /// 第三方包换版本时测试不至于跟着改。
    /// </remarks>
    [Fact]
    public void AddXiHanWebMcp_WhenExposable_RegistersSkillToolsConfigurator()
    {
        var services = new ServiceCollection();

        services.AddXiHanWebMcp(BuildConfiguration(enabled: true, apiKey: "valid-key"));

        Assert.Contains(services, descriptor => IsSkillToolsConfigurator(descriptor));
    }

    /// <summary>
    /// 未就绪时技能工具配置器也不进容器
    /// </summary>
    [Fact]
    public void AddXiHanWebMcp_WhenNotExposable_RegistersNoSkillToolsConfigurator()
    {
        var services = new ServiceCollection();

        services.AddXiHanWebMcp(BuildConfiguration(enabled: true, apiKey: null));

        Assert.DoesNotContain(services, descriptor => IsSkillToolsConfigurator(descriptor));
    }

    /// <summary>
    /// 断言容器里没有任何来自 MCP 协议包的服务注册
    /// </summary>
    /// <param name="services">服务集合</param>
    private static void AssertNoMcpServiceRegistered(IServiceCollection services)
    {
        Assert.DoesNotContain(services, descriptor => DependsOnMcpProtocolTypes(descriptor));
    }

    /// <summary>
    /// 判断服务描述符是否是技能工具配置器
    /// </summary>
    /// <remarks>
    /// 键控描述符上读 <c>ImplementationType</c> 会直接抛，所以先排除键控注册。
    /// </remarks>
    /// <param name="descriptor">服务描述符</param>
    /// <returns>是否是技能工具配置器</returns>
    private static bool IsSkillToolsConfigurator(ServiceDescriptor descriptor)
    {
        return !descriptor.IsKeyedService
            && descriptor.ImplementationType?.FullName == "XiHan.Framework.AI.Mcp.SkillMcpToolsConfigurator";
    }

    /// <summary>
    /// 判断服务描述符的服务类型是否来自 MCP 协议包
    /// </summary>
    /// <param name="descriptor">服务描述符</param>
    /// <returns>是否来自 MCP 协议包</returns>
    private static bool DependsOnMcpProtocolTypes(ServiceDescriptor descriptor)
    {
        return Flatten(descriptor.ServiceType).Any(type =>
            type.Assembly.GetName().Name?.StartsWith("ModelContextProtocol", StringComparison.Ordinal) == true);
    }

    /// <summary>
    /// 展开类型自身与它的全部泛型实参
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>类型自身与泛型实参</returns>
    private static IEnumerable<Type> Flatten(Type type)
    {
        yield return type;

        foreach (var argument in type.GetGenericArguments())
        {
            foreach (var nested in Flatten(argument))
            {
                yield return nested;
            }
        }
    }

    /// <summary>
    /// 构造 MCP 配置节
    /// </summary>
    /// <param name="enabled">是否启用</param>
    /// <param name="apiKey">密钥</param>
    /// <param name="headerName">请求头名</param>
    /// <param name="path">端点路径</param>
    /// <returns>配置根</returns>
    private static IConfiguration BuildConfiguration(bool enabled, string? apiKey, string? headerName = null, string? path = null)
    {
        var settings = new Dictionary<string, string?>
        {
            [$"{XiHanMcpOptions.SectionName}:Enabled"] = enabled ? "true" : "false"
        };

        if (apiKey is not null)
        {
            settings[$"{XiHanMcpOptions.SectionName}:ApiKey"] = apiKey;
        }

        if (headerName is not null)
        {
            settings[$"{XiHanMcpOptions.SectionName}:HeaderName"] = headerName;
        }

        if (path is not null)
        {
            settings[$"{XiHanMcpOptions.SectionName}:Path"] = path;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }
}
