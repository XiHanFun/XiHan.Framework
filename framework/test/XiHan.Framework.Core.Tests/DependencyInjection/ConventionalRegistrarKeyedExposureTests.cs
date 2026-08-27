// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;

namespace XiHan.Framework.Core.Tests.DependencyInjection;

/// <summary>
/// 约定注册的键值暴露分组语义测试
/// </summary>
/// <remarks>
/// 约定注册按服务 Key 把暴露类型分成若干组，重定向只在组内进行。
/// 这是刻意的：重定向后的键值描述器走 GetKeyedService(重定向类型, key) 解析，
/// 跨组重定向在容器里根本解析不到。本组用例把这条边界锁死，
/// 同时验证「把实现类型一并列进同一组」这个共享实例的正解确实有效。
/// </remarks>
public class ConventionalRegistrarKeyedExposureTests
{
    /// <summary>
    /// 键值门面与非键值门面是两条独立注册，各持一个实例
    /// </summary>
    [Fact]
    public void Resolve_WhenKeyedAndPlainExposed_DoesNotShareInstance()
    {
        IServiceCollection services = new ServiceCollection();
        new DefaultConventionalRegistrar().AddType(services, typeof(CkxDualService));

        using var provider = services.BuildServiceProvider();
        var keyed = provider.GetRequiredKeyedService<ICkxKeyed>("dual");
        var plain = provider.GetRequiredService<ICkxPlain>();

        Assert.IsType<CkxDualService>(keyed);
        Assert.IsType<CkxDualService>(plain);
        Assert.NotSame(keyed, plain);
    }

    /// <summary>
    /// 两组各自直接携带实现类型，没有发生跨组重定向
    /// </summary>
    [Fact]
    public void AddType_WhenKeyedAndPlainExposed_KeepsBothDescriptorsDirect()
    {
        IServiceCollection services = new ServiceCollection();

        new DefaultConventionalRegistrar().AddType(services, typeof(CkxDualService));

        var keyedDescriptor = Assert.Single(services.Where(d => d.ServiceType == typeof(ICkxKeyed)));
        var plainDescriptor = Assert.Single(services.Where(d => d.ServiceType == typeof(ICkxPlain)));
        // 单组只有一个暴露类型时不触发重定向，描述器直接携带实现类型而不是工厂
        Assert.Equal(typeof(CkxDualService), keyedDescriptor.KeyedImplementationType);
        Assert.Equal(typeof(CkxDualService), plainDescriptor.ImplementationType);
    }

    /// <summary>
    /// 同一 Key 下把实现类型一并暴露即可共享同一实例
    /// </summary>
    /// <remarks>这是需要「同一单例的多个门面」时的正解：重定向会指向组内的实现类型注册。</remarks>
    [Fact]
    public void Resolve_WhenKeyedGroupExposesImplementationType_SharesInstance()
    {
        IServiceCollection services = new ServiceCollection();
        new DefaultConventionalRegistrar().AddType(services, typeof(CkxSharedKeyedService));

        using var provider = services.BuildServiceProvider();
        var facade = provider.GetRequiredKeyedService<ICkxSharedAlpha>("shared");
        var self = provider.GetRequiredKeyedService<CkxSharedKeyedService>("shared");

        Assert.Same(self, facade);
    }

    /// <summary>
    /// 不同 Key 的键值暴露也各成一组，互不共享
    /// </summary>
    /// <remarks>边界：分组依据是 Key 本身，同一实现类型在两个 Key 下是两条注册。</remarks>
    [Fact]
    public void Resolve_WhenExposedUnderTwoKeys_DoesNotShareInstance()
    {
        IServiceCollection services = new ServiceCollection();
        new DefaultConventionalRegistrar().AddType(services, typeof(CkxTwoKeyService));

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredKeyedService<ICkxFirstKeyed>("first");
        var second = provider.GetRequiredKeyedService<ICkxSecondKeyed>("second");

        Assert.NotSame(first, second);
    }
}

/// <summary>
/// 键值与非键值并存样例的键值契约
/// </summary>
internal interface ICkxKeyed;

/// <summary>
/// 键值与非键值并存样例的普通契约
/// </summary>
internal interface ICkxPlain;

/// <summary>
/// 同组共享样例的门面契约
/// </summary>
internal interface ICkxSharedAlpha;

/// <summary>
/// 双 Key 样例的第一个契约
/// </summary>
internal interface ICkxFirstKeyed;

/// <summary>
/// 双 Key 样例的第二个契约
/// </summary>
internal interface ICkxSecondKeyed;

/// <summary>
/// 同时声明键值与非键值暴露的单例样例服务
/// </summary>
[ExposeServices(typeof(ICkxPlain))]
[ExposeKeyedServiceAttribute<ICkxKeyed>("dual")]
internal class CkxDualService : ICkxPlain, ICkxKeyed, ISingletonDependency;

/// <summary>
/// 在同一 Key 下同时暴露门面契约与自身的单例样例服务
/// </summary>
[ExposeKeyedServiceAttribute<ICkxSharedAlpha>("shared")]
[ExposeKeyedServiceAttribute<CkxSharedKeyedService>("shared")]
internal class CkxSharedKeyedService : ICkxSharedAlpha, ISingletonDependency;

/// <summary>
/// 在两个不同 Key 下各暴露一个契约的单例样例服务
/// </summary>
[ExposeKeyedServiceAttribute<ICkxFirstKeyed>("first")]
[ExposeKeyedServiceAttribute<ICkxSecondKeyed>("second")]
internal class CkxTwoKeyService : ICkxFirstKeyed, ICkxSecondKeyed, ISingletonDependency;
