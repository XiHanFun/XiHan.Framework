// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Grpc.AspNetCore.Server;
using Grpc.AspNetCore.Server.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Web.Grpc.Extensions.DependencyInjection;

namespace XiHan.Framework.Web.Grpc.Tests.Extensions.DependencyInjection;

/// <summary>
/// 曦寒 Web gRPC 服务集合扩展测试
/// </summary>
/// <remarks>
/// AddXiHanWebGrpc 是本项目对外唯一的注册入口，断言口径落在
/// 「注册后容器里有什么、生命周期是什么、重复注册是否幂等、选项回调是否生效」。
/// 只针对 Grpc.AspNetCore 的公共契约类型断言，不锁死其内部实现类型——那属于第三方实现细节。
/// </remarks>
public class XiHanWebGrpcServiceCollectionExtensionsTests
{
    /// <summary>
    /// 服务集合为空时抛出参数异常
    /// </summary>
    [Fact]
    public void AddXiHanWebGrpc_WhenServicesNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            XiHanWebGrpcServiceCollectionExtensions.AddXiHanWebGrpc(null!);
        });

        Assert.Equal("services", exception.ParamName);
    }

    /// <summary>
    /// 注册后返回同一个服务集合实例，保证链式调用语义
    /// </summary>
    [Fact]
    public void AddXiHanWebGrpc_Always_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var returned = services.AddXiHanWebGrpc();

        Assert.Same(services, returned);
    }

    /// <summary>
    /// 注册后 gRPC 服务激活器以开放泛型单例形式登记
    /// </summary>
    [Fact]
    public void AddXiHanWebGrpc_Registers_ServiceActivatorAsOpenGenericSingleton()
    {
        var services = new ServiceCollection();

        services.AddXiHanWebGrpc();

        List<ServiceDescriptor> descriptors = [.. services.Where(item => item.ServiceType == typeof(IGrpcServiceActivator<>))];
        ServiceDescriptor descriptor = Assert.Single(descriptors);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    /// <summary>
    /// 注册后 gRPC 服务方法提供器以开放泛型单例形式登记
    /// </summary>
    /// <remarks>
    /// 方法提供器是 MapGrpcService 发现服务方法的唯一来源，缺失会导致端点映射时找不到任何方法。
    /// </remarks>
    [Fact]
    public void AddXiHanWebGrpc_Registers_ServiceMethodProviderAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddXiHanWebGrpc();

        List<ServiceDescriptor> descriptors = [.. services.Where(item => item.ServiceType == typeof(IServiceMethodProvider<>))];
        Assert.NotEmpty(descriptors);
        Assert.All(descriptors, item => Assert.Equal(ServiceLifetime.Singleton, item.Lifetime));
    }

    /// <summary>
    /// 注册顺带引入路由核心服务，gRPC 端点依赖它
    /// </summary>
    /// <remarks>
    /// 按命名空间前缀断言而不是具体路由类型，避免锁死 ASP.NET Core 内部注册清单。
    /// </remarks>
    [Fact]
    public void AddXiHanWebGrpc_Registers_RoutingCoreServices()
    {
        var services = new ServiceCollection();

        services.AddXiHanWebGrpc();

        Assert.Contains(services, item => item.ServiceType.Namespace is not null
            && item.ServiceType.Namespace.StartsWith("Microsoft.AspNetCore.Routing", StringComparison.Ordinal));
    }

    /// <summary>
    /// 注册后 gRPC 服务选项可从容器解析
    /// </summary>
    [Fact]
    public void AddXiHanWebGrpc_Then_GrpcServiceOptionsIsResolvable()
    {
        var services = new ServiceCollection();
        services.AddXiHanWebGrpc();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<GrpcServiceOptions>>();

        Assert.NotNull(options.Value);
    }

    /// <summary>
    /// 注册之后追加的消息大小配置能生效
    /// </summary>
    /// <param name="messageSize">消息字节上限</param>
    [Theory]
    [InlineData(1024)]
    [InlineData(4 * 1024 * 1024)]
    [InlineData(int.MaxValue)]
    public void AddXiHanWebGrpc_ConfiguredMessageSize_IsHonored(int messageSize)
    {
        var services = new ServiceCollection();
        services.AddXiHanWebGrpc();
        services.Configure<GrpcServiceOptions>(options =>
        {
            options.MaxReceiveMessageSize = messageSize;
            options.MaxSendMessageSize = messageSize;
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<GrpcServiceOptions>>().Value;

        Assert.Equal(messageSize, options.MaxReceiveMessageSize);
        Assert.Equal(messageSize, options.MaxSendMessageSize);
    }

    /// <summary>
    /// 注册之后追加的开关型配置能生效
    /// </summary>
    [Fact]
    public void AddXiHanWebGrpc_ConfiguredSwitches_AreHonored()
    {
        var services = new ServiceCollection();
        services.AddXiHanWebGrpc();
        services.Configure<GrpcServiceOptions>(options =>
        {
            options.EnableDetailedErrors = true;
            options.IgnoreUnknownServices = true;
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<GrpcServiceOptions>>().Value;

        Assert.True(options.EnableDetailedErrors);
        Assert.True(options.IgnoreUnknownServices);
    }

    /// <summary>
    /// 重复注册不会让本扩展关心的登记翻倍，容器仍能正常解析
    /// </summary>
    /// <remarks>
    /// 这里刻意不断言「描述符总数完全不变」。本扩展自身没有任何注册逻辑，只是转调 AddGrpc()，
    /// 而 Grpc.AspNetCore 内部并非全部走 TryAdd，重复调用会多出几条属于它自己的登记（实测 4 条）。
    /// 那是第三方包的行为，锁死总数等于把它的实现细节固化进本仓测试，
    /// 上游一改版本就无故变红。真正属于本扩展的契约是：重复调用不抛异常、
    /// 关键服务不出现重复登记、容器仍能解析——只断言这三点。
    /// </remarks>
    [Fact]
    public void AddXiHanWebGrpc_CalledTwice_KeepsKeyRegistrationsUnique()
    {
        // 基准：只调一次时各关键服务各有几条登记
        var once = new ServiceCollection();
        once.AddXiHanWebGrpc();
        var expectedMethodProviders = once.Count(item => item.ServiceType == typeof(IServiceMethodProvider<>));

        var services = new ServiceCollection();
        services.AddXiHanWebGrpc();
        services.AddXiHanWebGrpc();

        Assert.Single(services.Where(item => item.ServiceType == typeof(IGrpcServiceActivator<>)).ToList());
        // IServiceMethodProvider<> 本就有多个实现（经 TryAddEnumerable 登记），
        // 断言的是「重复调用没有让它翻倍」，而不是它只有一条
        Assert.Equal(expectedMethodProviders, services.Count(item => item.ServiceType == typeof(IServiceMethodProvider<>)));

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IOptions<GrpcServiceOptions>>());
    }

    /// <summary>
    /// 重复注册后服务激活器仍只有一条登记
    /// </summary>
    [Fact]
    public void AddXiHanWebGrpc_CalledTwice_ServiceActivatorStaysSingleRegistration()
    {
        var services = new ServiceCollection();

        services.AddXiHanWebGrpc();
        services.AddXiHanWebGrpc();

        Assert.Single(services.Where(item => item.ServiceType == typeof(IGrpcServiceActivator<>)).ToList());
    }

    /// <summary>
    /// 注册不会破坏调用方已有的登记
    /// </summary>
    [Fact]
    public void AddXiHanWebGrpc_Keeps_PreexistingRegistrations()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IExistingService>(new ExistingService());

        services.AddXiHanWebGrpc();

        using var provider = services.BuildServiceProvider();
        Assert.IsType<ExistingService>(provider.GetRequiredService<IExistingService>());
    }

    /// <summary>
    /// 服务激活器在启用作用域校验的容器中可解析且为单例
    /// </summary>
    [Fact]
    public void AddXiHanWebGrpc_Then_ServiceActivatorResolvesAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddXiHanWebGrpc();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var first = provider.GetRequiredService<IGrpcServiceActivator<SampleGrpcService>>();
        var second = provider.GetRequiredService<IGrpcServiceActivator<SampleGrpcService>>();

        Assert.Same(first, second);
    }

    /// <summary>
    /// 用于闭合 gRPC 泛型注册的样板服务类型
    /// </summary>
    public sealed class SampleGrpcService
    {
    }

    /// <summary>
    /// 调用方已有登记的样板契约
    /// </summary>
    private interface IExistingService
    {
    }

    /// <summary>
    /// 调用方已有登记的样板实现
    /// </summary>
    private sealed class ExistingService : IExistingService
    {
    }
}
