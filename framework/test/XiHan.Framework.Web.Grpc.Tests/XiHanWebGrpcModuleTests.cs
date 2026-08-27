// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Grpc.AspNetCore.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Serialization;
using XiHan.Framework.Web.Core;

namespace XiHan.Framework.Web.Grpc.Tests;

/// <summary>
/// 曦寒框架 Web gRPC 服务端模块测试
/// </summary>
/// <remarks>
/// 模块本身没有状态，公共契约只有三条：依赖声明、服务配置阶段注册了什么、初始化阶段对宿主的硬性要求。
/// 初始化阶段的用例直接驱动 <see cref="XiHanModule"/> 的公共方法，不起真实 Web 主机——
/// 模块只从容器里取应用构建器，用手写替身即可覆盖全部分支。
/// </remarks>
public class XiHanWebGrpcModuleTests
{
    /// <summary>
    /// 模块只声明 Web 核心与序列化两个依赖
    /// </summary>
    [Fact]
    public void Module_DeclaresDependsOn_WebCoreAndSerializationOnly()
    {
        var dependedTypes = typeof(XiHanWebGrpcModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .SelectMany(attribute => attribute.GetDependedTypes())
            .ToList();

        Assert.Contains(typeof(XiHanWebCoreModule), dependedTypes);
        Assert.Contains(typeof(XiHanSerializationModule), dependedTypes);
        Assert.Equal(2, dependedTypes.Count);
    }

    /// <summary>
    /// 模块接入模块化生命周期契约
    /// </summary>
    [Fact]
    public void Module_Implements_ModuleLifecycleContracts()
    {
        var module = new XiHanWebGrpcModule();

        Assert.IsAssignableFrom<XiHanModule>(module);
        Assert.IsAssignableFrom<IXiHanModule>(module);
        Assert.IsAssignableFrom<IOnApplicationInitialization>(module);
    }

    /// <summary>
    /// 服务配置阶段完成 gRPC 服务注册
    /// </summary>
    [Fact]
    public void ConfigureServices_Registers_GrpcServices()
    {
        var services = new ServiceCollection();
        var module = new XiHanWebGrpcModule();

        module.ConfigureServices(new ServiceConfigurationContext(services));

        Assert.Contains(services, item => item.ServiceType == typeof(IGrpcServiceActivator<>));

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<IOptions<GrpcServiceOptions>>().Value);
    }

    /// <summary>
    /// 异步服务配置与同步版本产出相同的注册结果
    /// </summary>
    [Fact]
    public async Task ConfigureServicesAsync_ProducesSameRegistrationsAsSync()
    {
        var syncServices = new ServiceCollection();
        new XiHanWebGrpcModule().ConfigureServices(new ServiceConfigurationContext(syncServices));

        var asyncServices = new ServiceCollection();
        await new XiHanWebGrpcModule().ConfigureServicesAsync(new ServiceConfigurationContext(asyncServices));

        Assert.Equal(syncServices.Count, asyncServices.Count);
        Assert.Contains(asyncServices, item => item.ServiceType == typeof(IGrpcServiceActivator<>));
    }

    /// <summary>
    /// 同一上下文重复走服务配置不产生重复注册
    /// </summary>
    /// <remarks>
    /// 同上：本模块自身不做注册，只转调 AddGrpc()，而 Grpc.AspNetCore 并非全部走 TryAdd，
    /// 重复调用会多出属于它自己的几条登记。断言口径因此放在「关键服务不重复且可解析」，
    /// 而不是锁死描述符总数——后者锁的是第三方包的实现细节。
    /// </remarks>
    [Fact]
    public void ConfigureServices_CalledTwice_KeepsKeyRegistrationsUnique()
    {
        var services = new ServiceCollection();
        var context = new ServiceConfigurationContext(services);
        var module = new XiHanWebGrpcModule();

        module.ConfigureServices(context);
        module.ConfigureServices(context);

        Assert.Single(services.Where(item => item.ServiceType == typeof(IGrpcServiceActivator<>)).ToList());
    }

    /// <summary>
    /// 服务配置阶段不向模块间共享字典写入内容
    /// </summary>
    [Fact]
    public void ConfigureServices_DoesNotWriteContextItems()
    {
        var context = new ServiceConfigurationContext(new ServiceCollection());

        new XiHanWebGrpcModule().ConfigureServices(context);

        Assert.Empty(context.Items);
    }

    /// <summary>
    /// 应用构建器可用时初始化通过，且不向管道追加中间件
    /// </summary>
    /// <remarks>
    /// 当前实现只取出应用构建器而未注册任何中间件或端点，此断言锁定现状；
    /// 若后续在模块内补上 gRPC 管道装配，需要同步调整该用例。
    /// </remarks>
    [Fact]
    public void OnApplicationInitialization_WhenApplicationBuilderAvailable_RegistersNoMiddleware()
    {
        var accessor = new ObjectAccessor<IApplicationBuilder>();
        var services = new ServiceCollection();
        services.AddSingleton<IObjectAccessor<IApplicationBuilder>>(accessor);

        using var provider = services.BuildServiceProvider();
        var applicationBuilder = new RecordingApplicationBuilder(provider);
        accessor.Value = applicationBuilder;

        new XiHanWebGrpcModule().OnApplicationInitialization(new ApplicationInitializationContext(provider));

        Assert.Empty(applicationBuilder.Middlewares);
    }

    /// <summary>
    /// 容器里没有应用构建器访问器时初始化直接失败
    /// </summary>
    [Fact]
    public void OnApplicationInitialization_WhenAccessorMissing_ThrowsInvalidOperation()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var module = new XiHanWebGrpcModule();

        Assert.Throws<InvalidOperationException>(
            () => module.OnApplicationInitialization(new ApplicationInitializationContext(provider)));
    }

    /// <summary>
    /// 应用构建器访问器存在但值为空时抛出参数异常
    /// </summary>
    [Fact]
    public void OnApplicationInitialization_WhenApplicationBuilderIsNull_ThrowsArgumentNull()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IObjectAccessor<IApplicationBuilder>>(new ObjectAccessor<IApplicationBuilder>());

        using var provider = services.BuildServiceProvider();
        var module = new XiHanWebGrpcModule();

        var exception = Assert.Throws<ArgumentNullException>(
            () => module.OnApplicationInitialization(new ApplicationInitializationContext(provider)));
        Assert.Equal("applicationBuilder", exception.ParamName);
    }

    /// <summary>
    /// 异步初始化保持与同步版本一致的失败语义
    /// </summary>
    [Fact]
    public async Task OnApplicationInitializationAsync_WhenAccessorMissing_ThrowsInvalidOperation()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var module = new XiHanWebGrpcModule();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => module.OnApplicationInitializationAsync(new ApplicationInitializationContext(provider)));
    }

    /// <summary>
    /// 其余生命周期钩子保持空实现，不依赖应用构建器
    /// </summary>
    /// <remarks>
    /// 这几个钩子在真实装配里会在没有 Web 管道的场景下被调用，必须保证不抛异常也不写服务集合。
    /// </remarks>
    [Fact]
    public void RemainingLifecycleHooks_AreNoOps()
    {
        var services = new ServiceCollection();
        var configurationContext = new ServiceConfigurationContext(services);
        var module = new XiHanWebGrpcModule();

        module.PreConfigureServices(configurationContext);
        module.PostConfigureServices(configurationContext);

        Assert.Empty(services);

        using var provider = new ServiceCollection().BuildServiceProvider();
        var initializationContext = new ApplicationInitializationContext(provider);

        module.OnPreApplicationInitialization(initializationContext);
        module.OnPostApplicationInitialization(initializationContext);
        module.OnApplicationShutdown(new ApplicationShutdownContext(provider));

        Assert.Empty(configurationContext.Items);
    }

    /// <summary>
    /// 记录中间件注册动作的应用构建器替身
    /// </summary>
    private sealed class RecordingApplicationBuilder : IApplicationBuilder
    {
        private readonly List<Func<RequestDelegate, RequestDelegate>> _middlewares = [];

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="applicationServices">应用服务提供者</param>
        public RecordingApplicationBuilder(IServiceProvider applicationServices)
        {
            ApplicationServices = applicationServices;
        }

        /// <summary>
        /// 应用服务提供者
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
        /// 已登记的中间件
        /// </summary>
        public IReadOnlyList<Func<RequestDelegate, RequestDelegate>> Middlewares => _middlewares;

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
        /// <returns>新构建器</returns>
        public IApplicationBuilder New()
        {
            return new RecordingApplicationBuilder(ApplicationServices);
        }

        /// <summary>
        /// 追加中间件
        /// </summary>
        /// <param name="middleware">中间件</param>
        /// <returns>当前构建器</returns>
        public IApplicationBuilder Use(Func<RequestDelegate, RequestDelegate> middleware)
        {
            _middlewares.Add(middleware);
            return this;
        }
    }
}
