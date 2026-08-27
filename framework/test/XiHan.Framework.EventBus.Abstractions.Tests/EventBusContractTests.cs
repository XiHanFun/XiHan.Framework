// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Abstractions.Local;

namespace XiHan.Framework.EventBus.Abstractions.Tests;

/// <summary>
/// 事件总线接口契约测试
/// </summary>
/// <remarks>
/// 抽象包的产出物只有接口形状，调用方（含外部实现者）依赖的是继承关系、可选参数默认值与返回类型，
/// 这些一旦漂移会让已编译的下游程序集在运行期出现行为差异，因此这里用反射把它们钉死。
/// 反射统一带 <see cref="BindingFlags.DeclaredOnly"/>，只看当前接口自身声明的成员。
/// </remarks>
public class EventBusContractTests
{
    private const BindingFlags DeclaredMembers =
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    /// <summary>
    /// 本地事件总线继承通用事件总线
    /// </summary>
    [Fact]
    public void LocalEventBus_DerivesFromEventBus()
    {
        Assert.Contains(typeof(IEventBus), typeof(ILocalEventBus).GetInterfaces());
    }

    /// <summary>
    /// 分布式事件总线继承通用事件总线
    /// </summary>
    [Fact]
    public void DistributedEventBus_DerivesFromEventBus()
    {
        Assert.Contains(typeof(IEventBus), typeof(IDistributedEventBus).GetInterfaces());
    }

    /// <summary>
    /// 通用发布默认在工作单元完成后再投递
    /// </summary>
    [Fact]
    public void EventBusPublishAsync_OnUnitOfWorkComplete_DefaultsToTrue()
    {
        var method = typeof(IEventBus)
            .GetMethods(DeclaredMembers)
            .Single(x => x.Name == "PublishAsync" && x.IsGenericMethodDefinition);

        var parameters = method.GetParameters();

        Assert.Equal(2, parameters.Length);
        Assert.Equal("onUnitOfWorkComplete", parameters[1].Name);
        Assert.True(parameters[1].HasDefaultValue);

        var defaultValue = parameters[1].DefaultValue;
        Assert.NotNull(defaultValue);
        Assert.True((bool)defaultValue);
    }

    /// <summary>
    /// 非泛型发布同样默认在工作单元完成后再投递
    /// </summary>
    [Fact]
    public void EventBusPublishAsync_NonGenericOverload_KeepsSameDefault()
    {
        var method = typeof(IEventBus)
            .GetMethods(DeclaredMembers)
            .Single(x => x.Name == "PublishAsync" && !x.IsGenericMethodDefinition);

        var parameters = method.GetParameters();

        Assert.Equal(3, parameters.Length);
        Assert.Equal(typeof(Type), parameters[0].ParameterType);
        Assert.Equal(typeof(object), parameters[1].ParameterType);
        Assert.Equal("onUnitOfWorkComplete", parameters[2].Name);

        var defaultValue = parameters[2].DefaultValue;
        Assert.NotNull(defaultValue);
        Assert.True((bool)defaultValue);
    }

    /// <summary>
    /// 分布式发布默认走发件箱，保证可靠投递是缺省语义
    /// </summary>
    [Fact]
    public void DistributedPublishAsync_UseOutbox_DefaultsToTrue()
    {
        var method = typeof(IDistributedEventBus)
            .GetMethods(DeclaredMembers)
            .Single(x => x.Name == "PublishAsync" && x.IsGenericMethodDefinition);

        var parameters = method.GetParameters();

        Assert.Equal(3, parameters.Length);
        Assert.Equal("onUnitOfWorkComplete", parameters[1].Name);
        Assert.Equal("useOutbox", parameters[2].Name);

        var useOutbox = parameters[2].DefaultValue;
        Assert.NotNull(useOutbox);
        Assert.True((bool)useOutbox);
    }

    /// <summary>
    /// 分布式非泛型发布的发件箱默认值与泛型版本一致
    /// </summary>
    [Fact]
    public void DistributedPublishAsync_NonGenericOverload_KeepsSameDefaults()
    {
        var method = typeof(IDistributedEventBus)
            .GetMethods(DeclaredMembers)
            .Single(x => x.Name == "PublishAsync" && !x.IsGenericMethodDefinition);

        var parameters = method.GetParameters();

        Assert.Equal(4, parameters.Length);
        Assert.Equal("useOutbox", parameters[3].Name);

        var onUnitOfWorkComplete = parameters[2].DefaultValue;
        var useOutbox = parameters[3].DefaultValue;

        Assert.NotNull(onUnitOfWorkComplete);
        Assert.NotNull(useOutbox);
        Assert.True((bool)onUnitOfWorkComplete);
        Assert.True((bool)useOutbox);
    }

    /// <summary>
    /// 订阅返回可释放句柄，释放即注销
    /// </summary>
    [Theory]
    [InlineData(typeof(IEventHandler))]
    [InlineData(typeof(IEventHandlerFactory))]
    public void Subscribe_ByEventType_ReturnsDisposable(Type secondParameterType)
    {
        var method = typeof(IEventBus).GetMethod("Subscribe", [typeof(Type), secondParameterType]);

        Assert.NotNull(method);
        Assert.Equal(typeof(IDisposable), method.ReturnType);
    }

    /// <summary>
    /// 注销接口不返回句柄，只做副作用
    /// </summary>
    [Fact]
    public void UnsubscribeAll_ByEventType_ReturnsVoid()
    {
        var method = typeof(IEventBus).GetMethod("UnsubscribeAll", [typeof(Type)]);

        Assert.NotNull(method);
        Assert.Equal(typeof(void), method.ReturnType);
    }

    /// <summary>
    /// 本地事件总线暴露订阅表查询能力，供事件总线实现与诊断使用
    /// </summary>
    [Fact]
    public void LocalEventBus_ExposesHandlerFactoryLookup()
    {
        var method = typeof(ILocalEventBus).GetMethod(
            nameof(ILocalEventBus.GetEventHandlerFactories),
            [typeof(Type)]);

        Assert.NotNull(method);
        Assert.Equal(typeof(List<EventTypeWithEventHandlerFactories>), method.ReturnType);
    }

    /// <summary>
    /// 发布类方法一律异步，返回 Task
    /// </summary>
    [Fact]
    public void PublishAsync_AllOverloads_ReturnTask()
    {
        var methods = typeof(IEventBus)
            .GetMethods(DeclaredMembers)
            .Where(x => x.Name == "PublishAsync")
            .Concat(typeof(IDistributedEventBus)
                .GetMethods(DeclaredMembers)
                .Where(x => x.Name == "PublishAsync"))
            .ToList();

        Assert.Equal(4, methods.Count);
        Assert.All(methods, x => Assert.Equal(typeof(Task), x.ReturnType));
    }
}
