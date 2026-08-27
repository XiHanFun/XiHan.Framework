// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Web.Core.Extensions;
using XiHan.Framework.Web.Core.Extensions.DependencyInjection;

namespace XiHan.Framework.Web.Core.Tests.Extensions;

/// <summary>
/// 应用初始化上下文扩展测试
/// </summary>
/// <remarks>
/// 模块在 OnApplicationInitialization 里就是靠这组扩展拿管线构建器与环境的，
/// 每个取值方法都有"必须拿到"和"允许为空"两个版本，成对覆盖它们的差异：
/// 前者缺失时抛异常，后者缺失时返回 null。
/// </remarks>
public class ApplicationInitializationContextExtensionsTests
{
    /// <summary>
    /// 对象访问器里已放入管线构建器时两个取值方法都返回它
    /// </summary>
    [Fact]
    public void GetApplicationBuilder_WhenAccessorHasValue_ReturnsIt()
    {
        var services = new ServiceCollection();
        var accessor = services.AddObjectAccessor<IApplicationBuilder>();
        using var provider = services.BuildServiceProvider();
        var applicationBuilder = new ApplicationBuilder(provider);
        accessor.Value = applicationBuilder;

        var context = new ApplicationInitializationContext(provider);

        Assert.Same(applicationBuilder, context.GetApplicationBuilder());
        Assert.Same(applicationBuilder, context.GetApplicationBuilderOrNull());
    }

    /// <summary>
    /// 访问器已注册但还没赋值时，强取版抛参数空异常，可空版返回 null
    /// </summary>
    [Fact]
    public void GetApplicationBuilder_WhenAccessorHasNoValue_ThrowsButOrNullReturnsNull()
    {
        var services = new ServiceCollection();
        services.AddObjectAccessor<IApplicationBuilder>();
        using var provider = services.BuildServiceProvider();

        var context = new ApplicationInitializationContext(provider);

        Assert.Null(context.GetApplicationBuilderOrNull());

        var exception = Assert.Throws<ArgumentNullException>(() => context.GetApplicationBuilder());

        Assert.Equal("applicationBuilder", exception.ParamName);
    }

    /// <summary>
    /// 连对象访问器都没注册时属于装配缺陷，直接抛容器解析异常
    /// </summary>
    [Fact]
    public void GetApplicationBuilder_WhenAccessorNotRegistered_ThrowsResolveException()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        var context = new ApplicationInitializationContext(provider);

        Assert.Throws<InvalidOperationException>(() => context.GetApplicationBuilder());
        Assert.Throws<InvalidOperationException>(() => context.GetApplicationBuilderOrNull());
    }

    /// <summary>
    /// 已注册主机环境时两个取值方法都返回同一实例
    /// </summary>
    [Fact]
    public void GetEnvironment_WhenRegistered_ReturnsIt()
    {
        var environment = new EmptyHostingEnvironment
        {
            EnvironmentName = Environments.Staging,
            ContentRootPath = "/srv/app"
        };

        var services = new ServiceCollection();
        services.AddSingleton<IWebHostEnvironment>(environment);
        using var provider = services.BuildServiceProvider();

        var context = new ApplicationInitializationContext(provider);

        Assert.Same(environment, context.GetEnvironment());
        Assert.Same(environment, context.GetEnvironmentOrNull());
    }

    /// <summary>
    /// 未注册主机环境时，强取版抛异常，可空版返回 null
    /// </summary>
    [Fact]
    public void GetEnvironment_WhenNotRegistered_ThrowsButOrNullReturnsNull()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        var context = new ApplicationInitializationContext(provider);

        Assert.Null(context.GetEnvironmentOrNull());
        Assert.Throws<InvalidOperationException>(() => context.GetEnvironment());
    }

    /// <summary>
    /// 取配置返回容器里登记的那一份
    /// </summary>
    [Fact]
    public void GetConfiguration_ReturnsRegisteredConfiguration()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["XiHan:Demo"] = "1" })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        using var provider = services.BuildServiceProvider();

        var context = new ApplicationInitializationContext(provider);

        Assert.Same(configuration, context.GetConfiguration());
        Assert.Equal("1", context.GetConfiguration()["XiHan:Demo"]);
    }

    /// <summary>
    /// 未注册配置时抛容器解析异常
    /// </summary>
    [Fact]
    public void GetConfiguration_WhenNotRegistered_Throws()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        var context = new ApplicationInitializationContext(provider);

        Assert.Throws<InvalidOperationException>(() => context.GetConfiguration());
    }

    /// <summary>
    /// 取日志工厂返回容器里登记的那一份
    /// </summary>
    [Fact]
    public void GetLoggerFactory_ReturnsRegisteredFactory()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        using var provider = services.BuildServiceProvider();

        var context = new ApplicationInitializationContext(provider);

        Assert.Same(provider.GetRequiredService<ILoggerFactory>(), context.GetLoggerFactory());
    }

    /// <summary>
    /// 未注册日志时抛容器解析异常
    /// </summary>
    [Fact]
    public void GetLoggerFactory_WhenNotRegistered_Throws()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        var context = new ApplicationInitializationContext(provider);

        Assert.Throws<InvalidOperationException>(() => context.GetLoggerFactory());
    }
}
