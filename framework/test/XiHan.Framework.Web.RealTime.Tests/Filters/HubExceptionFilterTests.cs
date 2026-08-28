// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using XiHan.Framework.Web.RealTime.Constants;
using XiHan.Framework.Web.RealTime.Filters;
using XiHan.Framework.Web.RealTime.Hubs;
using XiHan.Framework.Web.RealTime.Services;
using XiHan.Framework.Web.RealTime.Tests.Infrastructure;

namespace XiHan.Framework.Web.RealTime.Tests.Filters;

/// <summary>
/// Hub 异常过滤器测试
/// </summary>
/// <remarks>
/// 该过滤器的核心契约是「方法调用异常统一包成 <see cref="HubException"/>，且不把内部异常细节推给客户端」，
/// 而连接与断开两条生命周期只做日志、异常必须原样上抛。用例分别锁死这两种处置方式。
/// </remarks>
public class HubExceptionFilterTests
{
    /// <summary>
    /// 方法调用成功时结果原样返回
    /// </summary>
    [Fact]
    public async Task InvokeMethodAsync_WhenHubMethodSucceeds_ReturnsResultUntouched()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        using var hub = CreateHub();
        var filter = CreateFilter();
        var invocationContext = CreateInvocationContext(provider, hub);
        var expected = new object();

        var actual = await filter.InvokeMethodAsync(invocationContext, _ => ValueTask.FromResult<object?>(expected));

        Assert.Same(expected, actual);
    }

    /// <summary>
    /// 方法调用成功时下一个处理器只被调用一次且拿到原上下文
    /// </summary>
    [Fact]
    public async Task InvokeMethodAsync_InvokesNextExactlyOnceWithSameContext()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        using var hub = CreateHub();
        var filter = CreateFilter();
        var invocationContext = CreateInvocationContext(provider, hub);
        var invocationCount = 0;
        HubInvocationContext? observed = null;

        await filter.InvokeMethodAsync(invocationContext, context =>
        {
            invocationCount++;
            observed = context;
            return ValueTask.FromResult<object?>(null);
        });

        Assert.Equal(1, invocationCount);
        Assert.Same(invocationContext, observed);
    }

    /// <summary>
    /// 方法调用抛异常时包装成携带方法名的 HubException
    /// </summary>
    [Fact]
    public async Task InvokeMethodAsync_WhenHubMethodThrows_WrapsIntoHubExceptionCarryingMethodName()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        using var hub = CreateHub();
        var filter = CreateFilter();
        var invocationContext = CreateInvocationContext(provider, hub);

        var hubException = await Assert.ThrowsAsync<HubException>(async () =>
            await filter.InvokeMethodAsync(
                invocationContext,
                _ => throw new InvalidOperationException("数据库连接串写错了")));

        Assert.Contains(SignalRConstants.ServerMethods.SendMessageToAll, hubException.Message);
    }

    /// <summary>
    /// 包装后的异常不把内部实现细节透给客户端
    /// </summary>
    /// <remarks>
    /// HubException 的消息会原样序列化给前端，泄漏内部异常文本等于把堆栈细节推到浏览器。
    /// </remarks>
    [Fact]
    public async Task InvokeMethodAsync_WhenHubMethodThrows_DoesNotLeakOriginalMessage()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        using var hub = CreateHub();
        var filter = CreateFilter();
        var invocationContext = CreateInvocationContext(provider, hub);

        var hubException = await Assert.ThrowsAsync<HubException>(async () =>
            await filter.InvokeMethodAsync(
                invocationContext,
                _ => throw new InvalidOperationException("数据库连接串写错了")));

        Assert.DoesNotContain("数据库连接串写错了", hubException.Message);
    }

    /// <summary>
    /// 已经是 HubException 的异常同样被重新包装
    /// </summary>
    [Fact]
    public async Task InvokeMethodAsync_WhenHubMethodThrowsHubException_StillRewraps()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        using var hub = CreateHub();
        var filter = CreateFilter();
        var invocationContext = CreateInvocationContext(provider, hub);
        var original = new HubException("业务侧想直接透传的提示");

        var hubException = await Assert.ThrowsAsync<HubException>(async () =>
            await filter.InvokeMethodAsync(invocationContext, _ => throw original));

        Assert.NotSame(original, hubException);
        Assert.Contains(SignalRConstants.ServerMethods.SendMessageToAll, hubException.Message);
    }

    /// <summary>
    /// 连接成功时调用下一个处理器
    /// </summary>
    [Fact]
    public async Task OnConnectedAsync_WhenNextSucceeds_InvokesNextWithSameContext()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        using var hub = CreateHub();
        var filter = CreateFilter();
        var lifetimeContext = CreateLifetimeContext(provider, hub);
        HubLifetimeContext? observed = null;

        await filter.OnConnectedAsync(lifetimeContext, context =>
        {
            observed = context;
            return Task.CompletedTask;
        });

        Assert.Same(lifetimeContext, observed);
    }

    /// <summary>
    /// 连接阶段异常原样上抛，不做包装
    /// </summary>
    /// <remarks>
    /// 连接阶段的异常要让 SignalR 自己走拒绝握手流程，包成 HubException 会改变连接失败的语义。
    /// </remarks>
    [Fact]
    public async Task OnConnectedAsync_WhenNextThrows_RethrowsSameException()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        using var hub = CreateHub();
        var filter = CreateFilter();
        var lifetimeContext = CreateLifetimeContext(provider, hub);
        var expected = new InvalidOperationException("握手校验失败");

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await filter.OnConnectedAsync(lifetimeContext, _ => throw expected));

        Assert.Same(expected, actual);
    }

    /// <summary>
    /// 断开连接时把断开原因原样转交下一个处理器
    /// </summary>
    [Fact]
    public async Task OnDisconnectedAsync_ForwardsDisconnectReasonToNext()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        using var hub = CreateHub();
        var filter = CreateFilter();
        var lifetimeContext = CreateLifetimeContext(provider, hub);
        var reason = new IOException("网络中断");
        Exception? observedReason = null;
        HubLifetimeContext? observedContext = null;

        await filter.OnDisconnectedAsync(lifetimeContext, reason, (context, exception) =>
        {
            observedContext = context;
            observedReason = exception;
            return Task.CompletedTask;
        });

        Assert.Same(lifetimeContext, observedContext);
        Assert.Same(reason, observedReason);
    }

    /// <summary>
    /// 正常断开时断开原因为 null 也能正常转交
    /// </summary>
    [Fact]
    public async Task OnDisconnectedAsync_WithoutReason_PassesNullThrough()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        using var hub = CreateHub();
        var filter = CreateFilter();
        var lifetimeContext = CreateLifetimeContext(provider, hub);
        var invoked = false;

        await filter.OnDisconnectedAsync(lifetimeContext, null, (_, exception) =>
        {
            invoked = true;
            Assert.Null(exception);
            return Task.CompletedTask;
        });

        Assert.True(invoked);
    }

    /// <summary>
    /// 断开阶段异常原样上抛，不做包装
    /// </summary>
    [Fact]
    public async Task OnDisconnectedAsync_WhenNextThrows_RethrowsSameException()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        using var hub = CreateHub();
        var filter = CreateFilter();
        var lifetimeContext = CreateLifetimeContext(provider, hub);
        var expected = new InvalidOperationException("清理失败");

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await filter.OnDisconnectedAsync(lifetimeContext, null, (_, _) => throw expected));

        Assert.Same(expected, actual);
    }

    /// <summary>
    /// 该过滤器可以直接挂到 SignalR 的 Hub 过滤器管线上
    /// </summary>
    [Fact]
    public void HubExceptionFilter_ImplementsHubFilterContract()
    {
        var filter = CreateFilter();

        Assert.IsAssignableFrom<IHubFilter>(filter);
    }

    /// <summary>
    /// 构造过滤器
    /// </summary>
    /// <returns></returns>
    private static HubExceptionFilter CreateFilter()
    {
        return new HubExceptionFilter(NullLogger<HubExceptionFilter>.Instance);
    }

    /// <summary>
    /// 构造被过滤的 Hub 实例
    /// </summary>
    /// <returns></returns>
    private static NotificationHub CreateHub()
    {
        return new NotificationHub(new ConnectionManager())
        {
            Context = new FakeHubCallerContext("conn-1", TestPrincipals.WithUserIdAndName("u1", "张三"))
        };
    }

    /// <summary>
    /// 构造指向 <see cref="NotificationHub.SendMessageToAll"/> 的方法调用上下文
    /// </summary>
    /// <param name="serviceProvider">服务提供器</param>
    /// <param name="hub">Hub 实例</param>
    /// <returns></returns>
    private static HubInvocationContext CreateInvocationContext(IServiceProvider serviceProvider, Hub hub)
    {
        var hubMethod = typeof(NotificationHub).GetMethod(
            nameof(NotificationHub.SendMessageToAll),
            BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(hubMethod);

        return new HubInvocationContext(
            new FakeHubCallerContext("conn-1", TestPrincipals.WithUserIdAndName("u1", "张三")),
            serviceProvider,
            hub,
            hubMethod,
            ["公告"]);
    }

    /// <summary>
    /// 构造生命周期上下文
    /// </summary>
    /// <param name="serviceProvider">服务提供器</param>
    /// <param name="hub">Hub 实例</param>
    /// <returns></returns>
    private static HubLifetimeContext CreateLifetimeContext(IServiceProvider serviceProvider, Hub hub)
    {
        return new HubLifetimeContext(
            new FakeHubCallerContext("conn-1", TestPrincipals.WithUserIdAndName("u1", "张三")),
            serviceProvider,
            hub);
    }
}
