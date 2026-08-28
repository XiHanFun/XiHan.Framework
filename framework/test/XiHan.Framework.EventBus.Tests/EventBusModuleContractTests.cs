// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Abstractions.Local;
using XiHan.Framework.EventBus.Distributed;
using XiHan.Framework.EventBus.Local;
using XiHan.Framework.Messaging;
using XiHan.Framework.Uow;
using XiHan.Framework.Uow.Abstracts;

namespace XiHan.Framework.EventBus.Tests;

/// <summary>
/// 事件总线模块装配契约测试
/// </summary>
/// <remarks>
/// 模块依赖声明与服务暴露特性决定了「谁被注册成什么、能不能被替换」，
/// 这些是运行期无法自证、改错了却只在集成阶段才炸的约定，所以在此锁死。
/// </remarks>
public class EventBusModuleContractTests
{
    /// <summary>
    /// 模块声明了运行所必需的四个前置模块
    /// </summary>
    [Fact]
    public void Module_DeclaresRequiredDependencies()
    {
        var dependsOn = typeof(XiHanEventBusModule).GetCustomAttribute<DependsOnAttribute>();

        Assert.NotNull(dependsOn);
        var depended = dependsOn!.GetDependedTypes();
        Assert.Contains(typeof(XiHanDistributedIdsModule), depended);
        Assert.Contains(typeof(XiHanEventBusAbstractionsModule), depended);
        Assert.Contains(typeof(XiHanMessagingModule), depended);
        Assert.Contains(typeof(XiHanUowModule), depended);
    }

    /// <summary>
    /// 模块是标准的曦寒模块
    /// </summary>
    [Fact]
    public void Module_DerivesFromXiHanModule()
    {
        Assert.True(typeof(XiHanModule).IsAssignableFrom(typeof(XiHanEventBusModule)));
    }

    /// <summary>
    /// 本地事件总线以接口和自身两种身份暴露且为单例
    /// </summary>
    [Fact]
    public void LocalEventBus_ExposesInterfaceAndSelfAsSingleton()
    {
        var exposeServices = typeof(LocalEventBus).GetCustomAttribute<ExposeServicesAttribute>();

        Assert.NotNull(exposeServices);
        Assert.Contains(typeof(ILocalEventBus), exposeServices!.ServiceTypes);
        Assert.Contains(typeof(LocalEventBus), exposeServices.ServiceTypes);
        Assert.True(typeof(ISingletonDependency).IsAssignableFrom(typeof(LocalEventBus)));
        Assert.True(typeof(EventBusBase).IsAssignableFrom(typeof(LocalEventBus)));
    }

    /// <summary>
    /// 本地分布式事件总线是可被具体中间件实现顶替的兜底注册
    /// </summary>
    /// <remarks>
    /// <c>TryRegister</c> 让 RabbitMQ / Kafka 等 Provider 先注册后即不再落地本地实现，
    /// 这条一旦丢失会把真实中间件实现覆盖掉，属于高杀伤改动。
    /// </remarks>
    [Fact]
    public void LocalDistributedEventBus_IsRegisteredAsFallback()
    {
        var dependency = typeof(LocalDistributedEventBus).GetCustomAttribute<DependencyAttribute>();

        Assert.NotNull(dependency);
        Assert.True(dependency!.TryRegister);
        Assert.False(dependency.ReplaceServices);
    }

    /// <summary>
    /// 本地分布式事件总线以接口和自身两种身份暴露且为单例
    /// </summary>
    [Fact]
    public void LocalDistributedEventBus_ExposesInterfaceAndSelfAsSingleton()
    {
        var exposeServices = typeof(LocalDistributedEventBus).GetCustomAttribute<ExposeServicesAttribute>();

        Assert.NotNull(exposeServices);
        Assert.Contains(typeof(IDistributedEventBus), exposeServices!.ServiceTypes);
        Assert.Contains(typeof(LocalDistributedEventBus), exposeServices.ServiceTypes);
        Assert.True(typeof(ISingletonDependency).IsAssignableFrom(typeof(LocalDistributedEventBus)));
        Assert.True(typeof(DistributedEventBusBase).IsAssignableFrom(typeof(LocalDistributedEventBus)));
    }

    /// <summary>
    /// 分布式事件总线基类同时承担事件盒契约
    /// </summary>
    [Fact]
    public void DistributedEventBusBase_SupportsEventBoxes()
    {
        Assert.True(typeof(ISupportsEventBoxes).IsAssignableFrom(typeof(DistributedEventBusBase)));
        Assert.True(typeof(IDistributedEventBus).IsAssignableFrom(typeof(DistributedEventBusBase)));
    }

    /// <summary>
    /// 面向消息中间件的基类仍是分布式事件总线，且必须由 Provider 派生
    /// </summary>
    [Fact]
    public void BrokerDistributedEventBusBase_IsAbstractDistributedEventBus()
    {
        Assert.True(typeof(BrokerDistributedEventBusBase).IsAbstract);
        Assert.True(typeof(DistributedEventBusBase).IsAssignableFrom(typeof(BrokerDistributedEventBusBase)));
    }

    /// <summary>
    /// 工作单元事件发布者替换默认实现且为瞬时生命周期
    /// </summary>
    [Fact]
    public void UnitOfWorkEventPublisher_ReplacesDefaultImplementation()
    {
        var dependency = typeof(UnitOfWorkEventPublisher).GetCustomAttribute<DependencyAttribute>();

        Assert.NotNull(dependency);
        Assert.True(dependency!.ReplaceServices);
        Assert.True(typeof(IUnitOfWorkEventPublisher).IsAssignableFrom(typeof(UnitOfWorkEventPublisher)));
        Assert.True(typeof(ITransientDependency).IsAssignableFrom(typeof(UnitOfWorkEventPublisher)));
    }

    /// <summary>
    /// 事件处理器调用器是可共享的单例
    /// </summary>
    /// <remarks>
    /// 调用器内部按「处理器类型 + 事件类型」缓存反射执行器，只有单例复用才有意义。
    /// </remarks>
    [Fact]
    public void EventHandlerInvoker_IsSingletonContract()
    {
        Assert.True(typeof(IEventHandlerInvoker).IsAssignableFrom(typeof(EventHandlerInvoker)));
        Assert.True(typeof(ISingletonDependency).IsAssignableFrom(typeof(EventHandlerInvoker)));
    }
}
