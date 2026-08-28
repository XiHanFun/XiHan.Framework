// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection;

namespace XiHan.Framework.Core.Tests.DependencyInjection;

/// <summary>
/// 属性字段自动装配处理器并发测试
/// </summary>
/// <remarks>
/// 处理器在框架内部以单例注册，同一实例会被多线程并发调用，
/// 而它内部按类型缓存编译好的赋值委托。缓存必须能承受并发写入：
/// 多个线程同时装配互不相同的类型时，既不能抛异常，也不能丢缓存项。
/// 用例借助泛型目标类型批量造出多个不同的运行时类型，逼缓存在并发下扩容。
/// </remarks>
public class AutowiredServiceHandlerConcurrencyTests
{
    /// <summary>
    /// 并发装配多个不同类型时全部注入成功
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Autowired_WhenManyTypesAutowiredConcurrently_InjectsEveryTarget()
    {
        using var provider = BuildProvider();
        var handler = new AutowiredServiceHandler(provider);
        var expected = provider.GetRequiredService<IAwcDependency>();
        var targets = CreateTargets();

        Parallel.ForEach(targets, target => handler.Autowired(target));

        Assert.All(targets, target => Assert.Same(expected, target.Injected));
    }

    /// <summary>
    /// 并发预热之后缓存依然可用
    /// </summary>
    /// <remarks>并发写坏字典时最典型的后果是缓存项丢失或读取异常，这里在预热之后再单线程走一次缓存命中路径。</remarks>
    [Fact(Timeout = 60_000)]
    public void Autowired_AfterConcurrentWarmUp_StillInjectsFromCache()
    {
        using var provider = BuildProvider();
        var handler = new AutowiredServiceHandler(provider);
        var expected = provider.GetRequiredService<IAwcDependency>();
        Parallel.ForEach(CreateTargets(), target => handler.Autowired(target));

        var late = new AwcTarget<int>();
        handler.Autowired(late);

        Assert.Same(expected, late.Injected);
    }

    /// <summary>
    /// 目标服务未注册时并发装配写入空值而不抛出
    /// </summary>
    /// <remarks>反例：装配不到服务是既有的容忍行为，并发下也不能变成异常。</remarks>
    [Fact(Timeout = 60_000)]
    public void Autowired_WhenServiceMissingAndConcurrent_AssignsNullWithoutThrowing()
    {
        IServiceCollection services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();
        var handler = new AutowiredServiceHandler(provider);
        var targets = CreateTargets();

        Parallel.ForEach(targets, target => handler.Autowired(target));

        Assert.All(targets, target => Assert.Null(target.Injected));
    }

    /// <summary>
    /// 构建注册了被装配服务的服务提供器
    /// </summary>
    /// <returns>服务提供器</returns>
    private static ServiceProvider BuildProvider()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IAwcDependency, AwcDependency>();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 造出一批分属多个不同运行时类型的装配目标
    /// </summary>
    /// <returns>装配目标数组</returns>
    private static IAwcProbe[] CreateTargets()
    {
        List<IAwcProbe> targets = [];

        for (var round = 0; round < 8; round++)
        {
            IAwcProbe[] batch =
            [
                new AwcTarget<int>(),
                new AwcTarget<long>(),
                new AwcTarget<short>(),
                new AwcTarget<byte>(),
                new AwcTarget<string>(),
                new AwcTarget<object>(),
                new AwcTarget<double>(),
                new AwcTarget<float>(),
                new AwcTarget<decimal>(),
                new AwcTarget<char>(),
                new AwcTarget<bool>(),
                new AwcTarget<Guid>()
            ];

            targets.AddRange(batch);
        }

        return [.. targets];
    }
}

/// <summary>
/// 并发装配用例的被注入契约
/// </summary>
internal interface IAwcDependency;

/// <summary>
/// 并发装配用例的被注入实现
/// </summary>
internal class AwcDependency : IAwcDependency;

/// <summary>
/// 供断言读取装配结果的探针契约
/// </summary>
internal interface IAwcProbe
{
    /// <summary>
    /// 装配后写入的依赖，未装配到时为空
    /// </summary>
    IAwcDependency? Injected { get; }
}

/// <summary>
/// 并发装配目标，靠泛型参数区分出多个不同的运行时类型
/// </summary>
/// <typeparam name="TMarker">仅用于区分运行时类型的占位参数</typeparam>
internal class AwcTarget<TMarker> : IAwcProbe
{
    /// <summary>
    /// 带标记的属性
    /// </summary>
    [AutowiredService]
    public IAwcDependency? MarkedProperty { get; set; }

    /// <summary>
    /// 装配结果
    /// </summary>
    public IAwcDependency? Injected => MarkedProperty;
}
