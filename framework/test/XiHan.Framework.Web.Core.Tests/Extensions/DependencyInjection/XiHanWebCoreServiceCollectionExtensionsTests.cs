// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Security.Claims;
using XiHan.Framework.Web.Core.Clients;
using XiHan.Framework.Web.Core.Extensions.DependencyInjection;
using XiHan.Framework.Web.Core.Options;
using XiHan.Framework.Web.Core.Security.Claims;

namespace XiHan.Framework.Web.Core.Tests.Extensions.DependencyInjection;

/// <summary>
/// 曦寒 Web 核心服务集合扩展测试
/// </summary>
/// <remarks>
/// 这组扩展决定了 Web 内核往容器里放什么、放什么生命周期，
/// 生命周期一旦错位（比如把作用域的主体访问器提成单例）会造成跨请求串号，属于安全问题，逐条锁死；
/// GetHostingEnvironment 只能看见"以实例方式注册"的环境，这是服务注册阶段的固有限制，一并写成用例说清楚。
/// </remarks>
public class XiHanWebCoreServiceCollectionExtensionsTests
{
    /// <summary>
    /// 未注册主机环境时回退成开发环境的空实现，而不是抛异常
    /// </summary>
    [Fact]
    public void GetHostingEnvironment_WhenNothingRegistered_FallsBackToDevelopment()
    {
        var environment = new ServiceCollection().GetHostingEnvironment();

        Assert.IsType<EmptyHostingEnvironment>(environment);
        Assert.Equal(Environments.Development, environment.EnvironmentName);
    }

    /// <summary>
    /// 以实例方式注册主机环境时原样返回该实例
    /// </summary>
    [Fact]
    public void GetHostingEnvironment_WhenInstanceRegistered_ReturnsThatInstance()
    {
        var registered = new EmptyHostingEnvironment { EnvironmentName = Environments.Staging };
        var services = new ServiceCollection();
        services.AddSingleton<IWebHostEnvironment>(registered);

        Assert.Same(registered, services.GetHostingEnvironment());
    }

    /// <summary>
    /// 只按类型注册主机环境时仍然回退成空实现
    /// </summary>
    /// <remarks>
    /// 服务注册阶段容器还没建好，扩展只能读服务描述器上的实例，按类型注册的实现此刻还不存在，
    /// 用"环境名是 Development 而不是 null"反证走的是回退分支。
    /// </remarks>
    [Fact]
    public void GetHostingEnvironment_WhenRegisteredByTypeOnly_StillFallsBack()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWebHostEnvironment, EmptyHostingEnvironment>();

        Assert.Equal(Environments.Development, services.GetHostingEnvironment().EnvironmentName);
    }

    /// <summary>
    /// 服务集合为空引用时抛参数空异常
    /// </summary>
    [Fact]
    public void AddXiHanWebCore_WhenServicesIsNull_Throws()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<ArgumentNullException>(
            () => XiHanWebCoreServiceCollectionExtensions.AddXiHanWebCore(null!, configuration));

        Assert.Equal("services", exception.ParamName);
    }

    /// <summary>
    /// 配置为空引用时抛参数空异常
    /// </summary>
    [Fact]
    public void AddXiHanWebCore_WhenConfigurationIsNull_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => new ServiceCollection().AddXiHanWebCore(null!));

        Assert.Equal("configuration", exception.ParamName);
    }

    /// <summary>
    /// 注册四类核心服务，且生命周期符合约定
    /// </summary>
    [Fact]
    public void AddXiHanWebCore_RegistersCoreServicesWithExpectedLifetimes()
    {
        var services = new ServiceCollection();

        var returned = services.AddXiHanWebCore(new ConfigurationBuilder().Build());

        Assert.Same(services, returned);

        var clientInfoProvider = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IClientInfoProvider));
        Assert.Equal(ServiceLifetime.Singleton, clientInfoProvider.Lifetime);
        Assert.Equal(typeof(HttpContextClientInfoProvider), clientInfoProvider.ImplementationType);

        var principalAccessor = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ICurrentPrincipalAccessor));
        Assert.Equal(ServiceLifetime.Scoped, principalAccessor.Lifetime);
        Assert.Equal(typeof(HttpContextCurrentPrincipalAccessor), principalAccessor.ImplementationType);

        var httpContextAccessor = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IHttpContextAccessor));
        Assert.Equal(ServiceLifetime.Singleton, httpContextAccessor.Lifetime);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IObjectAccessor<IApplicationBuilder>));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ObjectAccessor<IApplicationBuilder>));
    }

    /// <summary>
    /// 重复调用会把管线构建器的对象访问器注册两次，属于装配错误，直接抛异常暴露
    /// </summary>
    [Fact]
    public void AddXiHanWebCore_CalledTwice_Throws()
    {
        var services = new ServiceCollection();
        services.AddXiHanWebCore(new ConfigurationBuilder().Build());

        Assert.ThrowsAny<Exception>(() => services.AddXiHanWebCore(new ConfigurationBuilder().Build()));
    }

    /// <summary>
    /// 客户端信息配置按约定节名从传入的配置里绑定
    /// </summary>
    [Fact]
    public void AddXiHanWebCore_BindsClientInfoOptionsFromConfiguredSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["XiHan:Web:Core:ClientInfo:EnableIpRegion"] = "false",
                ["XiHan:Web:Core:ClientInfo:Ip2RegionDbPath"] = "custom/ip2region.xdb"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddXiHanWebCore(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<XiHanClientInfoOptions>>().Value;

        Assert.False(options.EnableIpRegion);
        Assert.Equal("custom/ip2region.xdb", options.Ip2RegionDbPath);
    }

    /// <summary>
    /// 补齐日志与主机环境后，注册的服务能真正从容器里解析出来
    /// </summary>
    [Fact]
    public void AddXiHanWebCore_RegisteredServicesAreResolvable()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IWebHostEnvironment>(new EmptyHostingEnvironment
        {
            EnvironmentName = Environments.Development,
            ApplicationName = "XiHan.Framework.Web.Core.Tests",
            ContentRootPath = AppContext.BaseDirectory
        });
        services.AddXiHanWebCore(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();

        Assert.IsType<HttpContextClientInfoProvider>(provider.GetRequiredService<IClientInfoProvider>());
        Assert.NotNull(provider.GetRequiredService<IHttpContextAccessor>());

        using var scope = provider.CreateScope();

        Assert.IsType<HttpContextCurrentPrincipalAccessor>(
            scope.ServiceProvider.GetRequiredService<ICurrentPrincipalAccessor>());
    }

    /// <summary>
    /// 客户端信息提供器是单例，两次解析拿到同一个实例
    /// </summary>
    [Fact]
    public void AddXiHanWebCore_ClientInfoProviderIsSingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IWebHostEnvironment>(new EmptyHostingEnvironment
        {
            EnvironmentName = Environments.Development,
            ContentRootPath = AppContext.BaseDirectory
        });
        services.AddXiHanWebCore(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<IClientInfoProvider>(), provider.GetRequiredService<IClientInfoProvider>());
    }

    /// <summary>
    /// 主体访问器是作用域级，跨作用域必须是不同实例，否则会串请求
    /// </summary>
    [Fact]
    public void AddXiHanWebCore_CurrentPrincipalAccessorIsScoped()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IWebHostEnvironment>(new EmptyHostingEnvironment
        {
            EnvironmentName = Environments.Development,
            ContentRootPath = AppContext.BaseDirectory
        });
        services.AddXiHanWebCore(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();

        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var first = firstScope.ServiceProvider.GetRequiredService<ICurrentPrincipalAccessor>();
        var second = secondScope.ServiceProvider.GetRequiredService<ICurrentPrincipalAccessor>();

        Assert.Same(first, firstScope.ServiceProvider.GetRequiredService<ICurrentPrincipalAccessor>());
        Assert.NotSame(first, second);
    }

    /// <summary>
    /// 声明转换以瞬时生命周期注册
    /// </summary>
    [Fact]
    public void TransformXiHanClaims_RegistersTransientClaimsTransformation()
    {
        var services = new ServiceCollection();

        var returned = services.TransformXiHanClaims();

        Assert.Same(services, returned);

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IClaimsTransformation));
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
        Assert.Equal(typeof(XiHanClaimsTransformation), descriptor.ImplementationType);
    }

    /// <summary>
    /// 解析出来的声明转换使用默认映射表，能把 sub 映射成用户标识
    /// </summary>
    [Fact]
    public async Task TransformXiHanClaims_ResolvedTransformationUsesDefaultMaps()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.TransformXiHanClaims();
        using var provider = services.BuildServiceProvider();

        var transformation = provider.GetRequiredService<IClaimsTransformation>();

        Assert.IsType<XiHanClaimsTransformation>(transformation);

        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "u-9")], "TestScheme"));
        var result = await transformation.TransformAsync(principal);

        Assert.Equal("u-9", result.FindFirst(XiHanClaimTypes.UserId)?.Value);
    }
}
