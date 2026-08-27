// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.Extensions.DependencyInjection;

namespace XiHan.Framework.Core.Tests.DependencyInjection;

/// <summary>
/// 根服务提供器测试
/// </summary>
/// <remarks>
/// 根服务提供器把「应用根容器」以单例形式暴露出来，其自身不做任何缓存或作用域管理，
/// 只是把请求原样转交给对象访问器里回填的那个提供器；解析结果必须来自根容器而非当前作用域。
/// </remarks>
public class RootServiceProviderTests
{
    /// <summary>
    /// 解析普通服务时转交给根容器
    /// </summary>
    [Fact]
    public void GetService_DelegatesToUnderlyingProvider()
    {
        using var provider = BuildProvider();
        var root = new RootServiceProvider(new ObjectAccessor<IServiceProvider>(provider));

        Assert.IsType<RspService>(root.GetService(typeof(IRspContract)));
    }

    /// <summary>
    /// 未注册服务时返回空
    /// </summary>
    [Fact]
    public void GetService_WhenNotRegistered_ReturnsNull()
    {
        using var provider = BuildProvider();
        var root = new RootServiceProvider(new ObjectAccessor<IServiceProvider>(provider));

        Assert.Null(root.GetService(typeof(RootServiceProviderTests)));
    }

    /// <summary>
    /// 解析键控服务时转交给根容器
    /// </summary>
    [Fact]
    public void GetKeyedService_DelegatesToUnderlyingProvider()
    {
        using var provider = BuildProvider();
        var root = new RootServiceProvider(new ObjectAccessor<IServiceProvider>(provider));

        Assert.IsType<RspService>(root.GetKeyedService(typeof(IRspContract), "keyed"));
        Assert.Null(root.GetKeyedService(typeof(IRspContract), "absent"));
    }

    /// <summary>
    /// 请求必需键控服务缺失时抛出
    /// </summary>
    [Fact]
    public void GetRequiredKeyedService_WhenMissing_Throws()
    {
        using var provider = BuildProvider();
        var root = new RootServiceProvider(new ObjectAccessor<IServiceProvider>(provider));

        Assert.IsType<RspService>(root.GetRequiredKeyedService(typeof(IRspContract), "keyed"));
        Assert.Throws<InvalidOperationException>(() => root.GetRequiredKeyedService(typeof(IRspContract), "absent"));
    }

    /// <summary>
    /// 单例服务经根提供器解析与直接解析得到同一实例
    /// </summary>
    [Fact]
    public void GetService_WhenSingleton_ReturnsSameInstanceAsRootContainer()
    {
        using var provider = BuildProvider();
        var root = new RootServiceProvider(new ObjectAccessor<IServiceProvider>(provider));

        Assert.Same(provider.GetRequiredService<IRspContract>(), root.GetService(typeof(IRspContract)));
    }

    /// <summary>
    /// 经约定注册后暴露为单例的根提供器契约
    /// </summary>
    [Fact]
    public void RootServiceProvider_RegisteredByConvention_IsSingleton()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IRspContract, RspService>();
        new DefaultConventionalRegistrar().AddType(services, typeof(RootServiceProvider));
        var accessor = services.AddObjectAccessor<IServiceProvider>();

        using var provider = services.BuildServiceProvider();
        accessor.Value = provider;

        var root = provider.GetRequiredService<IRootServiceProvider>();

        Assert.Same(root, provider.GetRequiredService<IRootServiceProvider>());
        Assert.Same(provider.GetRequiredService<IRspContract>(), root.GetService(typeof(IRspContract)));
    }

    /// <summary>
    /// 构建带普通与键控注册的服务提供器
    /// </summary>
    /// <returns>服务提供器</returns>
    private static ServiceProvider BuildProvider()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IRspContract, RspService>();
        services.AddKeyedSingleton<IRspContract, RspService>("keyed");
        return services.BuildServiceProvider();
    }
}

/// <summary>
/// 根服务提供器测试用契约
/// </summary>
internal interface IRspContract;

/// <summary>
/// 根服务提供器测试用实现
/// </summary>
internal class RspService : IRspContract;
