// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Tests.Fakes;

namespace XiHan.Framework.EventBus.Tests;

/// <summary>
/// 事件处理器释放包装器幂等性测试
/// </summary>
/// <remarks>
/// 锁住一条修复：Dispose 原来没有幂等保护，重复调用会重复执行构造时传入的归还动作。
/// 当前三个内置工厂传入的动作（scope.Dispose、(handler as IDisposable)?.Dispose）自身幂等，
/// 所以线上还没暴露问题，但 IDisposable 的契约是「多次 Dispose 与一次等价」，
/// 包装器不能把这个前提转嫁给调用方传入的任意动作——外部自定义工厂完全可以传一个「归还到池里」这类不幂等的动作，
/// 重复归还会把同一个实例放回池中两次。
/// 用计数委托而不是可释放替身来断言，才能区分「动作被调了两次」与「动作本身幂等」。
/// </remarks>
public class EventHandlerDisposeWrapperIdempotencyTests
{
    /// <summary>
    /// 重复释放只执行一次归还动作
    /// </summary>
    /// <remarks>
    /// 这是修复前必然失败的核心场景：修复前计数会是 2。
    /// </remarks>
    [Fact]
    public void Dispose_CalledTwice_InvokesActionOnce()
    {
        var invoked = 0;
        var wrapper = new EventHandlerDisposeWrapper(new ParameterlessLocalHandler(), () => invoked++);

        wrapper.Dispose();
        wrapper.Dispose();

        Assert.Equal(1, invoked);
    }

    /// <summary>
    /// 多次释放同样只执行一次归还动作
    /// </summary>
    /// <remarks>
    /// 幂等不是「只挡住第二次」，第三次、第四次都必须继续挡住。
    /// </remarks>
    [Fact]
    public void Dispose_CalledManyTimes_InvokesActionOnce()
    {
        var invoked = 0;
        var wrapper = new EventHandlerDisposeWrapper(new ParameterlessLocalHandler(), () => invoked++);

        for (var index = 0; index < 5; index++)
        {
            wrapper.Dispose();
        }

        Assert.Equal(1, invoked);
    }

    /// <summary>
    /// using 语句退出后再手动释放不会重复归还
    /// </summary>
    /// <remarks>
    /// 这是真实调用形态：EventBusBase.TriggerHandlerAsync 用 using 托管包装器，
    /// 调用方若在块内提前手动释放，块结束时的隐式释放不能再来一遍。
    /// </remarks>
    [Fact]
    public void Dispose_AfterUsingBlock_DoesNotInvokeActionAgain()
    {
        var invoked = 0;
        IEventHandlerDisposeWrapper wrapper =
            new EventHandlerDisposeWrapper(new ParameterlessLocalHandler(), () => invoked++);

        using (wrapper)
        {
            // 块内提前手动释放，块结束时的隐式释放是第二次调用
            wrapper.Dispose();
        }

        // 调用方在块外再拿到同一个包装器释放一次，仍不能重复归还
        wrapper.Dispose();

        Assert.Equal(1, invoked);
    }

    /// <summary>
    /// 归还动作自身抛异常后不会被重跑
    /// </summary>
    /// <remarks>
    /// 边界：先置标记再执行动作。若反过来写成「执行成功才置标记」，
    /// 动作抛异常时第二次 Dispose 会再抛一次，异常路径上的清理反而更危险。
    /// </remarks>
    [Fact]
    public void Dispose_WhenActionThrows_DoesNotRetryOnSecondDispose()
    {
        var invoked = 0;
        var wrapper = new EventHandlerDisposeWrapper(
            new ParameterlessLocalHandler(),
            () =>
            {
                invoked++;
                throw new InvalidOperationException("归还失败");
            });

        Assert.Throws<InvalidOperationException>(wrapper.Dispose);
        wrapper.Dispose();

        Assert.Equal(1, invoked);
    }

    /// <summary>
    /// 未提供归还动作时重复释放不抛异常
    /// </summary>
    /// <remarks>
    /// 反例：单实例工厂走的就是这条无动作路径，加了标记位不能把它变成会抛的。
    /// </remarks>
    [Fact]
    public void Dispose_WithoutActionCalledTwice_DoesNotThrow()
    {
        var wrapper = new EventHandlerDisposeWrapper(new ParameterlessLocalHandler());

        wrapper.Dispose();
        wrapper.Dispose();
    }

    /// <summary>
    /// 释放后处理器实例仍可读取
    /// </summary>
    /// <remarks>
    /// 反例：幂等标记只应拦住归还动作，不能顺手把 EventHandler 置空。
    /// 单实例工厂返回的包装器释放后，那个单例处理器本身仍然是活的。
    /// </remarks>
    [Fact]
    public void EventHandler_AfterDispose_StillReturnsSameHandler()
    {
        var handler = new ParameterlessLocalHandler();
        var wrapper = new EventHandlerDisposeWrapper(handler);

        wrapper.Dispose();
        wrapper.Dispose();

        Assert.Same(handler, wrapper.EventHandler);
    }

    /// <summary>
    /// 每个包装器实例各自记一份释放标记
    /// </summary>
    /// <remarks>
    /// 边界：标记必须是实例字段。若误写成静态字段，第一个包装器释放后
    /// 后续所有包装器的归还动作都会被静默吞掉——那才是真正的作用域泄漏。
    /// </remarks>
    [Fact]
    public void Dispose_OnSeparateWrappers_InvokesEachActionOnce()
    {
        var first = 0;
        var second = 0;
        var firstWrapper = new EventHandlerDisposeWrapper(new ParameterlessLocalHandler(), () => first++);
        var secondWrapper = new EventHandlerDisposeWrapper(new ParameterlessLocalHandler(), () => second++);

        firstWrapper.Dispose();
        firstWrapper.Dispose();
        secondWrapper.Dispose();
        secondWrapper.Dispose();

        Assert.Equal(1, first);
        Assert.Equal(1, second);
    }
}
