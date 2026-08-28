// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;
using XiHan.Framework.AI;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Web.Core;
using XiHan.Framework.Web.Mcp.Options;

namespace XiHan.Framework.Web.Mcp.Tests;

/// <summary>
/// 曦寒框架 Web MCP 模块测试
/// </summary>
/// <remarks>
/// 模块只做两件事：把配置绑进容器、在初始化阶段把端点挂上去。依赖声明决定了它能不能拿到
/// Web 管道与 AI 技能注册表，丢一个都会在运行期才炸，所以在这里锁死。
/// 初始化阶段有两条短路：宿主不是端点路由构建器时直接返回（非 Web 宿主复用该模块的场景），
/// 以及配置未就绪时不映射任何端点——两条都单独覆盖。
/// 就绪分支真正落到 <c>MapMcp</c>，需要 MCP 协议包的完整容器装配，属集成范畴，不在本工程覆盖。
/// </remarks>
public class XiHanWebMcpModuleTests
{
    /// <summary>
    /// 模块继承框架模块基类，才能被模块加载器识别
    /// </summary>
    [Fact]
    public void Module_IsXiHanModule()
    {
        var module = new XiHanWebMcpModule();

        Assert.IsAssignableFrom<XiHanModule>(module);
        Assert.IsAssignableFrom<IXiHanModule>(module);
    }

    /// <summary>
    /// 模块同时依赖 Web 核心模块与 AI 模块
    /// </summary>
    [Fact]
    public void Module_DependsOnWebCoreAndAiModules()
    {
        var attribute = typeof(XiHanWebMcpModule).GetCustomAttribute<DependsOnAttribute>(false);

        Assert.NotNull(attribute);

        var dependedTypes = attribute.GetDependedTypes();

        Assert.Equal(2, dependedTypes.Length);
        Assert.Contains(typeof(XiHanWebCoreModule), dependedTypes);
        Assert.Contains(typeof(XiHanAIModule), dependedTypes);
    }

    /// <summary>
    /// 服务配置阶段绑定 MCP 选项
    /// </summary>
    [Fact]
    public void ConfigureServices_WithConfiguration_BindsOptions()
    {
        var context = CreateServiceConfigurationContext(enabled: false, apiKey: "valid-key");

        new XiHanWebMcpModule().ConfigureServices(context);

        using var provider = context.Services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XiHanMcpOptions>>().Value;

        Assert.False(options.Enabled);
        Assert.Equal("valid-key", options.ApiKey);
        Assert.False(options.IsExposable);
    }

    /// <summary>
    /// 服务集合里没有配置实例时快速失败，而不是静默按默认值走
    /// </summary>
    [Fact]
    public void ConfigureServices_WithoutConfiguration_Throws()
    {
        var context = new ServiceConfigurationContext(new ServiceCollection());

        Assert.Throws<XiHanException>(() => new XiHanWebMcpModule().ConfigureServices(context));
    }

    /// <summary>
    /// 异步入口与同步入口行为一致
    /// </summary>
    [Fact]
    public async Task ConfigureServicesAsync_DelegatesToSyncOverload()
    {
        var context = CreateServiceConfigurationContext(enabled: false, apiKey: null);

        await new XiHanWebMcpModule().ConfigureServicesAsync(context);

        Assert.Contains(context.Services, descriptor => descriptor.ServiceType == typeof(IConfigureOptions<XiHanMcpOptions>));
    }

    /// <summary>
    /// 宿主的应用程序构建器不是端点路由构建器时直接返回，不读取任何配置
    /// </summary>
    /// <remarks>
    /// 容器里刻意不注册 <c>IOptions&lt;XiHanMcpOptions&gt;</c>：一旦短路失效，
    /// 后面那句 <c>GetRequiredService</c> 就会抛，用「不抛」来证明短路真的发生了。
    /// </remarks>
    [Fact]
    public void OnApplicationInitialization_WhenHostIsNotEndpointRouteBuilder_DoesNothing()
    {
        var accessor = new ObjectAccessor<IApplicationBuilder>();
        var services = new ServiceCollection();
        services.AddSingleton<IObjectAccessor<IApplicationBuilder>>(accessor);

        using var provider = services.BuildServiceProvider();
        accessor.Value = new ApplicationBuilder(provider);

        new XiHanWebMcpModule().OnApplicationInitialization(new ApplicationInitializationContext(provider));

        Assert.Null(provider.GetService<IOptions<XiHanMcpOptions>>());
    }

    /// <summary>
    /// 配置未就绪时初始化阶段不注册任何端点数据源
    /// </summary>
    /// <param name="enabled">是否启用</param>
    /// <param name="apiKey">配置的密钥</param>
    [Theory]
    [InlineData(false, null)]
    [InlineData(false, "valid-key")]
    [InlineData(true, null)]
    [InlineData(true, "  ")]
    public void OnApplicationInitialization_WhenNotExposable_MapsNoEndpoint(bool enabled, string? apiKey)
    {
        var accessor = new ObjectAccessor<IApplicationBuilder>();
        var services = new ServiceCollection();
        services.AddSingleton<IObjectAccessor<IApplicationBuilder>>(accessor);
        services.Configure<XiHanMcpOptions>(options =>
        {
            options.Enabled = enabled;
            options.ApiKey = apiKey;
        });

        using var provider = services.BuildServiceProvider();
        var host = new FakeEndpointRouteApplicationBuilder(provider);
        accessor.Value = host;

        new XiHanWebMcpModule().OnApplicationInitialization(new ApplicationInitializationContext(provider));

        Assert.Empty(host.DataSources);
    }

    /// <summary>
    /// 构造带 MCP 配置的服务配置上下文
    /// </summary>
    /// <param name="enabled">是否启用</param>
    /// <param name="apiKey">配置的密钥</param>
    /// <returns>服务配置上下文</returns>
    private static ServiceConfigurationContext CreateServiceConfigurationContext(bool enabled, string? apiKey)
    {
        var settings = new Dictionary<string, string?>
        {
            [$"{XiHanMcpOptions.SectionName}:Enabled"] = enabled ? "true" : "false"
        };

        if (apiKey is not null)
        {
            settings[$"{XiHanMcpOptions.SectionName}:ApiKey"] = apiKey;
        }

        var services = new ServiceCollection();

        // 模块的 ConfigureServices 直接从服务集合里取配置实例，缺了它会抛 XiHanException
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

        return new ServiceConfigurationContext(services);
    }

    /// <summary>
    /// 同时充当应用程序构建器与端点路由构建器的手写宿主替身
    /// </summary>
    private sealed class FakeEndpointRouteApplicationBuilder : IApplicationBuilder, IEndpointRouteBuilder
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="serviceProvider">服务提供者</param>
        public FakeEndpointRouteApplicationBuilder(IServiceProvider serviceProvider)
        {
            ApplicationServices = serviceProvider;
        }

        /// <summary>
        /// 应用程序服务
        /// </summary>
        public IServiceProvider ApplicationServices { get; set; }

        /// <summary>
        /// 服务器特性集合
        /// </summary>
        public IFeatureCollection ServerFeatures { get; } = new FeatureCollection();

        /// <summary>
        /// 构建器属性
        /// </summary>
        public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();

        /// <summary>
        /// 已注册的端点数据源
        /// </summary>
        public ICollection<EndpointDataSource> DataSources { get; } = [];

        /// <summary>
        /// 端点路由构建器使用的服务提供者
        /// </summary>
        public IServiceProvider ServiceProvider => ApplicationServices;

        /// <summary>
        /// 构建请求委托
        /// </summary>
        /// <returns>请求委托</returns>
        public RequestDelegate Build()
        {
            return _ => Task.CompletedTask;
        }

        /// <summary>
        /// 创建同源的新构建器
        /// </summary>
        /// <returns>新的应用程序构建器</returns>
        public IApplicationBuilder New()
        {
            return new FakeEndpointRouteApplicationBuilder(ApplicationServices);
        }

        /// <summary>
        /// 追加中间件，本替身不构建真实管道，直接返回自身
        /// </summary>
        /// <param name="middleware">中间件</param>
        /// <returns>自身</returns>
        public IApplicationBuilder Use(Func<RequestDelegate, RequestDelegate> middleware)
        {
            return this;
        }

        /// <summary>
        /// 创建应用程序构建器
        /// </summary>
        /// <returns>应用程序构建器</returns>
        public IApplicationBuilder CreateApplicationBuilder()
        {
            return New();
        }
    }
}
