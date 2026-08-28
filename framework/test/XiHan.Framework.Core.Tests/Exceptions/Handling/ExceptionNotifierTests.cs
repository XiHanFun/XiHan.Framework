// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Core.Exceptions.Abstracts;
using XiHan.Framework.Core.Exceptions.Handling;

namespace XiHan.Framework.Core.Tests.Exceptions.Handling;

/// <summary>
/// 异常通知器测试
/// </summary>
/// <remarks>
/// 通知器的编排职责有三条：每次通知开一个新作用域取订阅者、把同一个上下文交给每个订阅者、
/// 任一订阅者抛错都不能中断后面的订阅者。第三条最关键——异常通知本身发生在异常处理路径上，
/// 在这里再抛一次会把原始异常掩盖掉。
/// </remarks>
public class ExceptionNotifierTests
{
    /// <summary>
    /// 所有已注册订阅者都会收到同一个上下文
    /// </summary>
    [Fact]
    public async Task NotifyAsync_InvokesEverySubscriberWithSameContext()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<SubscriberCallLog>();
        services.AddTransient<IExceptionSubscriber, FirstRecordingSubscriber>();
        services.AddTransient<IExceptionSubscriber, SecondRecordingSubscriber>();

        using var provider = services.BuildServiceProvider();
        var log = provider.GetRequiredService<SubscriberCallLog>();
        var notifier = new ExceptionNotifier(provider.GetRequiredService<IServiceScopeFactory>());
        var context = new ExceptionNotificationContext(new InvalidOperationException("底层失败"));

        await notifier.NotifyAsync(context);

        Assert.Equal(
            [nameof(FirstRecordingSubscriber), nameof(SecondRecordingSubscriber)],
            log.HandlerNames);
        Assert.All(log.Contexts, handled => Assert.Same(context, handled));
    }

    /// <summary>
    /// 某个订阅者抛错时后续订阅者仍然被调用
    /// </summary>
    [Fact]
    public async Task NotifyAsync_WhenSubscriberThrows_ContinuesWithRemainingSubscribers()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<SubscriberCallLog>();
        services.AddTransient<IExceptionSubscriber, ThrowingSubscriber>();
        services.AddTransient<IExceptionSubscriber, SecondRecordingSubscriber>();

        using var provider = services.BuildServiceProvider();
        var log = provider.GetRequiredService<SubscriberCallLog>();
        var notifier = new ExceptionNotifier(provider.GetRequiredService<IServiceScopeFactory>());

        await notifier.NotifyAsync(new ExceptionNotificationContext(new InvalidOperationException("底层失败")));

        Assert.Equal(
            [nameof(ThrowingSubscriber), nameof(SecondRecordingSubscriber)],
            log.HandlerNames);
    }

    /// <summary>
    /// 没有任何订阅者时通知安静完成
    /// </summary>
    [Fact]
    public async Task NotifyAsync_WithoutSubscribers_CompletesQuietly()
    {
        IServiceCollection services = new ServiceCollection();

        using var provider = services.BuildServiceProvider();
        var notifier = new ExceptionNotifier(provider.GetRequiredService<IServiceScopeFactory>());

        await notifier.NotifyAsync(new ExceptionNotificationContext(new InvalidOperationException("底层失败")));
    }

    /// <summary>
    /// 上下文为空时抛出参数空异常并带上参数名
    /// </summary>
    [Fact]
    public async Task NotifyAsync_WithNullContext_ThrowsArgumentNullException()
    {
        IServiceCollection services = new ServiceCollection();

        using var provider = services.BuildServiceProvider();
        var notifier = new ExceptionNotifier(provider.GetRequiredService<IServiceScopeFactory>());

        var thrown = await Assert.ThrowsAsync<ArgumentNullException>(() => notifier.NotifyAsync(null!));

        Assert.Equal("context", thrown.ParamName);
    }

    /// <summary>
    /// 每次通知都开一个新作用域，作用域内的订阅者不会跨次复用
    /// </summary>
    [Fact]
    public async Task NotifyAsync_CreatesFreshScopePerCall()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<SubscriberCallLog>();
        services.AddScoped<IExceptionSubscriber, FirstRecordingSubscriber>();

        using var provider = services.BuildServiceProvider();
        var log = provider.GetRequiredService<SubscriberCallLog>();
        var notifier = new ExceptionNotifier(provider.GetRequiredService<IServiceScopeFactory>());
        var context = new ExceptionNotificationContext(new InvalidOperationException("底层失败"));

        await notifier.NotifyAsync(context);
        await notifier.NotifyAsync(context);

        Assert.Equal(2, log.Instances.Count);
        Assert.NotSame(log.Instances[0], log.Instances[1]);
    }

    /// <summary>
    /// 日志记录器默认是空实现，属性注入之前调用也不会空引用
    /// </summary>
    [Fact]
    public void Logger_DefaultsToNullLogger()
    {
        IServiceCollection services = new ServiceCollection();

        using var provider = services.BuildServiceProvider();
        var notifier = new ExceptionNotifier(provider.GetRequiredService<IServiceScopeFactory>());

        Assert.Same(NullLogger<ExceptionNotifier>.Instance, notifier.Logger);
    }

    /// <summary>
    /// 通知器落在通知契约上，并按瞬时生命周期参与约定注册
    /// </summary>
    [Fact]
    public void Type_ImplementsNotifierContractWithTransientLifetime()
    {
        Assert.True(typeof(IExceptionNotifier).IsAssignableFrom(typeof(ExceptionNotifier)));
        Assert.True(typeof(ITransientDependency).IsAssignableFrom(typeof(ExceptionNotifier)));
    }
}

/// <summary>
/// 记录订阅者调用顺序、上下文与实例的日志
/// </summary>
public sealed class SubscriberCallLog
{
    private readonly List<string> _handlerNames = [];
    private readonly List<ExceptionNotificationContext> _contexts = [];
    private readonly List<object> _instances = [];

    /// <summary>
    /// 按调用顺序记录的订阅者名称
    /// </summary>
    public IReadOnlyList<string> HandlerNames => _handlerNames;

    /// <summary>
    /// 按调用顺序记录的上下文
    /// </summary>
    public IReadOnlyList<ExceptionNotificationContext> Contexts => _contexts;

    /// <summary>
    /// 按调用顺序记录的订阅者实例
    /// </summary>
    public IReadOnlyList<object> Instances => _instances;

    /// <summary>
    /// 记录一次订阅者调用
    /// </summary>
    /// <param name="handlerName">订阅者名称</param>
    /// <param name="context">异常通知上下文</param>
    /// <param name="instance">订阅者实例</param>
    public void Record(string handlerName, ExceptionNotificationContext context, object instance)
    {
        _handlerNames.Add(handlerName);
        _contexts.Add(context);
        _instances.Add(instance);
    }
}

/// <summary>
/// 只做记录的订阅者
/// </summary>
public sealed class FirstRecordingSubscriber : IExceptionSubscriber
{
    private readonly SubscriberCallLog _log;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="log">调用日志</param>
    public FirstRecordingSubscriber(SubscriberCallLog log)
    {
        _log = log;
    }

    /// <summary>
    /// 处理异常
    /// </summary>
    /// <param name="context">异常通知上下文</param>
    /// <returns>已完成的任务</returns>
    public Task HandleAsync(ExceptionNotificationContext context)
    {
        _log.Record(nameof(FirstRecordingSubscriber), context, this);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 只做记录的第二个订阅者
/// </summary>
public sealed class SecondRecordingSubscriber : IExceptionSubscriber
{
    private readonly SubscriberCallLog _log;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="log">调用日志</param>
    public SecondRecordingSubscriber(SubscriberCallLog log)
    {
        _log = log;
    }

    /// <summary>
    /// 处理异常
    /// </summary>
    /// <param name="context">异常通知上下文</param>
    /// <returns>已完成的任务</returns>
    public Task HandleAsync(ExceptionNotificationContext context)
    {
        _log.Record(nameof(SecondRecordingSubscriber), context, this);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 记录之后立刻抛错的订阅者
/// </summary>
public sealed class ThrowingSubscriber : IExceptionSubscriber
{
    private readonly SubscriberCallLog _log;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="log">调用日志</param>
    public ThrowingSubscriber(SubscriberCallLog log)
    {
        _log = log;
    }

    /// <summary>
    /// 处理异常并抛错
    /// </summary>
    /// <param name="context">异常通知上下文</param>
    /// <returns>不会返回</returns>
    /// <exception cref="InvalidOperationException">固定抛出</exception>
    public Task HandleAsync(ExceptionNotificationContext context)
    {
        _log.Record(nameof(ThrowingSubscriber), context, this);
        throw new InvalidOperationException("订阅者故意失败");
    }
}
