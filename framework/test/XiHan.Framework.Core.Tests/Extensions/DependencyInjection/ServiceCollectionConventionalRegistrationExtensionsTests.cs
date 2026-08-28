// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Core.Extensions.DependencyInjection;

namespace XiHan.Framework.Core.Tests.Extensions.DependencyInjection;

/// <summary>
/// 服务集合常规注册扩展方法测试
/// </summary>
/// <remarks>
/// 这组扩展本身不做注册，只负责把「加程序集 / 加类型」广播给所有已登记的常规注册器。
/// 因此用例分两层：一层用手写注册器验证广播确实到达每一个注册器、参数原样透传；
/// 另一层保留默认注册器，验证"开箱即用"这条默认行为没被改掉。
/// 广播用例会先清空注册器列表，避免默认注册器顺带扫描整个测试程序集拖慢用例。
/// </remarks>
public class ServiceCollectionConventionalRegistrationExtensionsTests
{
    /// <summary>
    /// 首次获取时列表里只有默认注册器
    /// </summary>
    [Fact]
    public void GetConventionalRegistrars_DefaultsToSingleDefaultRegistrar()
    {
        IServiceCollection services = new ServiceCollection();

        var registrar = Assert.Single(services.GetConventionalRegistrars());

        Assert.IsType<DefaultConventionalRegistrar>(registrar);
    }

    /// <summary>
    /// 多次获取拿到同一份注册器列表
    /// </summary>
    [Fact]
    public void GetConventionalRegistrars_ReturnsSameListAcrossCalls()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Same(services.GetConventionalRegistrars(), services.GetConventionalRegistrars());
    }

    /// <summary>
    /// 追加注册器后进入列表，并返回同一个服务集合
    /// </summary>
    [Fact]
    public void AddConventionalRegistrar_AppendsAndReturnsSameCollection()
    {
        IServiceCollection services = new ServiceCollection();
        RecordingConventionalRegistrar registrar = new();

        var returned = services.AddConventionalRegistrar(registrar);

        Assert.Same(services, returned);
        Assert.Equal(2, services.GetConventionalRegistrars().Count);
        Assert.Same(registrar, services.GetConventionalRegistrars()[1]);
    }

    /// <summary>
    /// 加类型时广播给每一个注册器
    /// </summary>
    [Fact]
    public void AddType_BroadcastsToEveryRegistrar()
    {
        IServiceCollection services = new ServiceCollection();
        var first = ReplaceWithRecordingRegistrar(services);
        RecordingConventionalRegistrar second = new();
        services.AddConventionalRegistrar(second);

        var returned = services.AddType(typeof(ConventionSampleService));

        Assert.Same(services, returned);
        Assert.Equal(typeof(ConventionSampleService), Assert.Single(first.Types));
        Assert.Equal(typeof(ConventionSampleService), Assert.Single(second.Types));
    }

    /// <summary>
    /// 泛型加类型透传类型实参
    /// </summary>
    [Fact]
    public void AddTypeGeneric_PassesTypeArgument()
    {
        IServiceCollection services = new ServiceCollection();
        var registrar = ReplaceWithRecordingRegistrar(services);

        services.AddType<ConventionSampleService>();

        Assert.Equal(typeof(ConventionSampleService), Assert.Single(registrar.Types));
    }

    /// <summary>
    /// 批量加类型时原样透传整批类型
    /// </summary>
    [Fact]
    public void AddTypes_PassesEveryTypeThrough()
    {
        IServiceCollection services = new ServiceCollection();
        var registrar = ReplaceWithRecordingRegistrar(services);

        services.AddTypes(typeof(ConventionSampleService), typeof(OtherConventionSampleService));

        Assert.Equal(
            new[] { typeof(ConventionSampleService), typeof(OtherConventionSampleService) },
            registrar.Types);
    }

    /// <summary>
    /// 加程序集时把程序集原样广播出去
    /// </summary>
    [Fact]
    public void AddAssembly_PassesAssemblyThrough()
    {
        IServiceCollection services = new ServiceCollection();
        var registrar = ReplaceWithRecordingRegistrar(services);
        var assembly = typeof(ConventionSampleService).Assembly;

        services.AddAssembly(assembly);

        Assert.Same(assembly, Assert.Single(registrar.Assemblies));
    }

    /// <summary>
    /// 泛型加程序集取类型实参所在的程序集
    /// </summary>
    [Fact]
    public void AddAssemblyOf_UsesAssemblyOfTypeArgument()
    {
        IServiceCollection services = new ServiceCollection();
        var registrar = ReplaceWithRecordingRegistrar(services);

        services.AddAssemblyOf<ConventionSampleService>();

        Assert.Same(typeof(ConventionSampleService).Assembly, Assert.Single(registrar.Assemblies));
    }

    /// <summary>
    /// 默认注册器按生命周期标记接口完成约定注册
    /// </summary>
    /// <remarks>
    /// 这条不测默认注册器自身的分支细节，只确认"开箱不追加任何注册器就能工作"——
    /// 一旦默认注册器没被放进列表，所有模块的自动注册都会静默失效。
    /// </remarks>
    [Fact]
    public void AddType_WithDefaultRegistrar_RegistersLifetimeMarkedType()
    {
        IServiceCollection services = new ServiceCollection();

        services.AddType<ConventionSampleService>();

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IConventionSampleService));
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
        Assert.True(services.IsAdded<ConventionSampleService>());
    }

    /// <summary>
    /// 没有生命周期标记的类型不会被默认注册器登记
    /// </summary>
    [Fact]
    public void AddType_WithDefaultRegistrar_IgnoresUnmarkedType()
    {
        IServiceCollection services = new ServiceCollection();

        services.AddType<UnmarkedConventionSample>();

        Assert.False(services.IsAdded<UnmarkedConventionSample>());
    }

    /// <summary>
    /// 把注册器列表换成只含一个手写注册器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>手写注册器</returns>
    private static RecordingConventionalRegistrar ReplaceWithRecordingRegistrar(IServiceCollection services)
    {
        var registrars = services.GetConventionalRegistrars();
        registrars.Clear();

        RecordingConventionalRegistrar registrar = new();
        registrars.Add(registrar);

        return registrar;
    }
}

/// <summary>
/// 只记录广播内容、不做任何注册的常规注册器替身
/// </summary>
public sealed class RecordingConventionalRegistrar : IConventionalRegistrar
{
    /// <summary>
    /// 收到的程序集
    /// </summary>
    public List<Assembly> Assemblies { get; } = [];

    /// <summary>
    /// 收到的类型
    /// </summary>
    public List<Type> Types { get; } = [];

    /// <summary>
    /// 记录程序集
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="assembly">程序集</param>
    public void AddAssembly(IServiceCollection services, Assembly assembly)
    {
        Assemblies.Add(assembly);
    }

    /// <summary>
    /// 记录一批类型
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="types">类型集合</param>
    public void AddTypes(IServiceCollection services, params Type[] types)
    {
        Types.AddRange(types);
    }

    /// <summary>
    /// 记录单个类型
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="type">类型</param>
    public void AddType(IServiceCollection services, Type type)
    {
        Types.Add(type);
    }
}

/// <summary>
/// 常规注册测试用的服务契约
/// </summary>
public interface IConventionSampleService
{
    /// <summary>
    /// 返回固定值
    /// </summary>
    /// <returns>固定字符串</returns>
    string Ping();
}

/// <summary>
/// 带瞬时生命周期标记的常规注册样例
/// </summary>
public sealed class ConventionSampleService : IConventionSampleService, ITransientDependency
{
    /// <summary>
    /// 返回固定值
    /// </summary>
    /// <returns>固定字符串</returns>
    public string Ping()
    {
        return "convention";
    }
}

/// <summary>
/// 常规注册测试用的另一个样例
/// </summary>
public sealed class OtherConventionSampleService : IConventionSampleService, ITransientDependency
{
    /// <summary>
    /// 返回固定值
    /// </summary>
    /// <returns>固定字符串</returns>
    public string Ping()
    {
        return "other";
    }
}

/// <summary>
/// 没有任何生命周期标记的样例
/// </summary>
public sealed class UnmarkedConventionSample
{
    /// <summary>
    /// 返回固定值
    /// </summary>
    /// <returns>固定字符串</returns>
    public string Ping()
    {
        return "unmarked";
    }
}
