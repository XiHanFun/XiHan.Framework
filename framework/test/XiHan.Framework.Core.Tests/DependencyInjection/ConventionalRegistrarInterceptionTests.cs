// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Castle.Extensions;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Core.DynamicProxy;
using XiHan.Framework.Core.Extensions.DependencyInjection;

namespace XiHan.Framework.Core.Tests.DependencyInjection;

/// <summary>
/// 约定注册与动态代理协作的测试
/// </summary>
/// <remarks>
/// 覆盖约定注册对多暴露类型服务生成工厂描述器后，动态代理仍须为其创建代理这一契约。
/// </remarks>
public class ConventionalRegistrarInterceptionTests
{
    /// <summary>
    /// 作用域服务经约定注册后必须被拦截
    /// </summary>
    [Fact]
    public void ScopedService_RegisteredByConvention_IsIntercepted()
    {
        using var provider = BuildProvider(typeof(ScopedSample));

        var service = provider.GetRequiredService<IScopedSample>();
        var result = service.Ping();

        Assert.Equal("raw", result);
        Assert.True(ProxyUtil.IsProxy(service));
    }

    /// <summary>
    /// 单例服务经约定注册后必须被拦截
    /// </summary>
    [Fact]
    public void SingletonService_RegisteredByConvention_IsIntercepted()
    {
        using var provider = BuildProvider(typeof(SingletonSample));

        var service = provider.GetRequiredService<ISingletonSample>();
        service.Ping();

        Assert.True(ProxyUtil.IsProxy(service));
    }

    /// <summary>
    /// 瞬时服务经约定注册后必须被拦截
    /// </summary>
    [Fact]
    public void TransientService_RegisteredByConvention_IsIntercepted()
    {
        using var provider = BuildProvider(typeof(TransientSample));

        var service = provider.GetRequiredService<ITransientSample>();
        service.Ping();

        Assert.True(ProxyUtil.IsProxy(service));
    }

    /// <summary>
    /// 拦截器实际执行到被调用的方法上
    /// </summary>
    [Fact]
    public void ScopedService_Invocation_ReachesInterceptor()
    {
        var recorder = new InvocationRecorder();
        using var provider = BuildProvider(typeof(ScopedSample), recorder);

        provider.GetRequiredService<IScopedSample>().Ping();

        Assert.Contains(nameof(IScopedSample.Ping), recorder.MethodNames);
    }

    /// <summary>
    /// 代理与并存的自身注册共享同一目标实例
    /// </summary>
    [Fact]
    public void ScopedService_ProxyAndSelfRegistration_ShareSameTarget()
    {
        using var provider = BuildProvider(typeof(ScopedSample));
        using var scope = provider.CreateScope();

        var proxied = scope.ServiceProvider.GetRequiredService<IScopedSample>();
        var self = scope.ServiceProvider.GetRequiredService<ScopedSample>();

        Assert.Same(self, ProxyUtil.GetUnproxiedInstance(proxied));
    }

    /// <summary>
    /// 显式指定暴露类型的服务同样被拦截
    /// </summary>
    [Fact]
    public void ExplicitlyExposedService_IsIntercepted()
    {
        using var provider = BuildProvider(typeof(ExplicitSample));

        var service = provider.GetRequiredService<IExplicitSample>();

        Assert.True(ProxyUtil.IsProxy(service));
    }

    /// <summary>
    /// 构建注册了指定类型并启用动态代理的服务提供器
    /// </summary>
    /// <param name="type">待注册的实现类型</param>
    /// <param name="recorder">调用记录器</param>
    /// <returns>服务提供器</returns>
    private static ServiceProvider BuildProvider(Type type, InvocationRecorder? recorder = null)
    {
        var services = new ServiceCollection();

        services.AddSingleton(recorder ?? new InvocationRecorder());
        services.AddTransient<MarkerInterceptor>();
        services.OnRegistered(context => context.Interceptors.TryAdd<MarkerInterceptor>());

        new DefaultConventionalRegistrar().AddType(services, type);
        services.AddCastleDynamicProxy();

        return services.BuildServiceProvider();
    }
}

/// <summary>
/// 记录被拦截方法名
/// </summary>
public class InvocationRecorder
{
    private readonly List<string> _methodNames = [];

    /// <summary>
    /// 已记录的方法名
    /// </summary>
    public IReadOnlyList<string> MethodNames => _methodNames;

    /// <summary>
    /// 记录一次方法调用
    /// </summary>
    /// <param name="methodName">方法名</param>
    public void Record(string methodName)
    {
        _methodNames.Add(methodName);
    }
}

/// <summary>
/// 记录调用并放行的拦截器
/// </summary>
public class MarkerInterceptor : XiHanInterceptor
{
    private readonly InvocationRecorder _recorder;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="recorder">调用记录器</param>
    public MarkerInterceptor(InvocationRecorder recorder)
    {
        _recorder = recorder;
    }

    /// <summary>
    /// 拦截
    /// </summary>
    /// <param name="invocation">方法调用</param>
    /// <returns></returns>
    public override async Task InterceptAsync(IXiHanMethodInvocation invocation)
    {
        _recorder.Record(invocation.Method.Name);
        await invocation.ProceedAsync();
    }
}

/// <summary>
/// 作用域样例服务契约
/// </summary>
public interface IScopedSample
{
    /// <summary>
    /// 返回固定值
    /// </summary>
    /// <returns></returns>
    string Ping();
}

/// <summary>
/// 单例样例服务契约
/// </summary>
public interface ISingletonSample
{
    /// <summary>
    /// 返回固定值
    /// </summary>
    /// <returns></returns>
    string Ping();
}

/// <summary>
/// 瞬时样例服务契约
/// </summary>
public interface ITransientSample
{
    /// <summary>
    /// 返回固定值
    /// </summary>
    /// <returns></returns>
    string Ping();
}

/// <summary>
/// 显式暴露样例服务契约
/// </summary>
public interface IExplicitSample
{
    /// <summary>
    /// 返回固定值
    /// </summary>
    /// <returns></returns>
    string Ping();
}

/// <summary>
/// 作用域样例服务
/// </summary>
public class ScopedSample : IScopedSample, IScopedDependency
{
    /// <summary>
    /// 返回固定值
    /// </summary>
    /// <returns>固定字符串 raw</returns>
    public string Ping() => "raw";
}

/// <summary>
/// 单例样例服务
/// </summary>
public class SingletonSample : ISingletonSample, ISingletonDependency
{
    /// <summary>
    /// 返回固定值
    /// </summary>
    /// <returns>固定字符串 raw</returns>
    public string Ping() => "raw";
}

/// <summary>
/// 瞬时样例服务
/// </summary>
public class TransientSample : ITransientSample, ITransientDependency
{
    /// <summary>
    /// 返回固定值
    /// </summary>
    /// <returns>固定字符串 raw</returns>
    public string Ping() => "raw";
}

/// <summary>
/// 显式暴露样例服务
/// </summary>
[ExposeServices(typeof(IExplicitSample))]
public class ExplicitSample : IExplicitSample, IScopedDependency
{
    /// <summary>
    /// 返回固定值
    /// </summary>
    /// <returns>固定字符串 raw</returns>
    public string Ping() => "raw";
}
