// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.EventBus.Local;
using XiHan.Framework.EventBus.Tests.Fakes;

namespace XiHan.Framework.EventBus.Tests.Local;

/// <summary>
/// 本地事件总线选项与开箱订阅测试
/// </summary>
/// <remarks>
/// 选项里的处理器类型会在总线构造时被批量订阅（走 IoC 工厂解析），
/// 因此这里连同「构造即订阅」这条装配契约一起验证，而不是只断言选项字段默认值。
/// </remarks>
public class XiHanLocalEventBusOptionsTests
{
    /// <summary>
    /// 默认没有登记任何处理器类型
    /// </summary>
    [Fact]
    public void Handlers_IsEmptyByDefault()
    {
        var options = new XiHanLocalEventBusOptions();

        Assert.NotNull(options.Handlers);
        Assert.Empty(options.Handlers);
    }

    /// <summary>
    /// 两个选项实例之间的处理器列表相互独立
    /// </summary>
    [Fact]
    public void Handlers_AreNotSharedBetweenInstances()
    {
        var first = new XiHanLocalEventBusOptions();
        var second = new XiHanLocalEventBusOptions();

        first.Handlers.Add<ParameterlessLocalHandler>();

        Assert.Single(first.Handlers);
        Assert.Empty(second.Handlers);
    }

    /// <summary>
    /// 登记的处理器类型可被查询到
    /// </summary>
    [Fact]
    public void Handlers_AfterAdd_ContainsHandlerType()
    {
        var options = new XiHanLocalEventBusOptions();

        options.Handlers.Add<ParameterlessLocalHandler>();

        Assert.True(options.Handlers.Contains<ParameterlessLocalHandler>());
        Assert.Contains(typeof(ParameterlessLocalHandler), options.Handlers);
    }

    /// <summary>
    /// 总线构造时会把选项里声明的处理器订阅进去
    /// </summary>
    [Fact]
    public async Task Ctor_SubscribesHandlersDeclaredInOptions()
    {
        using var harness = LocalEventBusHarness.Create(
            services => services.AddSingleton<RecordingLocalHandler<PlainNoticeEvent>>(),
            options => options.Handlers.Add<RecordingLocalHandler<PlainNoticeEvent>>());

        await harness.Bus.PublishAsync(new PlainNoticeEvent { Message = "开箱订阅" });

        var handler = harness.Services.GetRequiredService<RecordingLocalHandler<PlainNoticeEvent>>();
        Assert.Single(handler.Received);
    }

    /// <summary>
    /// 选项为空时不产生任何订阅
    /// </summary>
    [Fact]
    public void Ctor_WithoutDeclaredHandlers_RegistersNothing()
    {
        using var harness = LocalEventBusHarness.Create();

        Assert.Empty(harness.Bus.GetEventHandlerFactories(typeof(PlainNoticeEvent)));
    }

    /// <summary>
    /// 同一处理器实现了多个处理器接口时只登记一次工厂
    /// </summary>
    /// <remarks>
    /// <c>DualChannelHandler</c> 同时是本地与分布式处理器，两个接口的泛型参数都是同一个事件类型，
    /// 批量订阅会尝试登记两次，IoC 工厂按处理器类型判重后应只保留一条。
    /// </remarks>
    [Fact]
    public void Ctor_WithHandlerImplementingMultipleInterfaces_RegistersFactoryOnce()
    {
        using var harness = LocalEventBusHarness.Create(
            services => services.AddSingleton<DualChannelHandler>(),
            options => options.Handlers.Add<DualChannelHandler>());

        Assert.Single(harness.Bus.GetEventHandlerFactories(typeof(PlainNoticeEvent)));
    }

    /// <summary>
    /// 选项声明的处理器由容器解析，同一单例实例跨多次发布保持状态
    /// </summary>
    [Fact]
    public async Task Ctor_ResolvesDeclaredHandlerFromContainer()
    {
        using var harness = LocalEventBusHarness.Create(
            services => services.AddSingleton<RecordingLocalHandler<PlainNoticeEvent>>(),
            options => options.Handlers.Add<RecordingLocalHandler<PlainNoticeEvent>>());

        await harness.Bus.PublishAsync(new PlainNoticeEvent { Message = "第一次" });
        await harness.Bus.PublishAsync(new PlainNoticeEvent { Message = "第二次" });

        var handler = harness.Services.GetRequiredService<RecordingLocalHandler<PlainNoticeEvent>>();
        Assert.Equal(2, handler.Received.Count);
    }
}
