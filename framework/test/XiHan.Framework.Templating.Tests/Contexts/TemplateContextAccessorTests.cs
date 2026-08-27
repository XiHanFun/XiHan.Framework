// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Templating.Contexts;

namespace XiHan.Framework.Templating.Tests.Contexts;

/// <summary>
/// <see cref="TemplateContextAccessor"/> 环境上下文语义的测试
/// </summary>
/// <remarks>
/// 该访问器用静态 <see cref="AsyncLocal{T}"/> 承载当前上下文，两条语义必须锁死：
/// 同一异步流内跨实例共享（这是它作为环境访问器的意义），
/// 子任务内的赋值不回流到调用方（这是它不会串上下文的保证）。
/// </remarks>
public class TemplateContextAccessorTests
{
    /// <summary>
    /// 在一个实例上赋值后，另一个实例读到同一个上下文
    /// </summary>
    [Fact]
    public void Current_SetOnOneInstance_IsVisibleFromAnotherInstance()
    {
        var first = new TemplateContextAccessor();
        var second = new TemplateContextAccessor();
        var context = new TemplateContext();

        try
        {
            first.Current = context;

            Assert.Same(context, second.Current);
        }
        finally
        {
            first.Current = null;
        }
    }

    /// <summary>
    /// 置为 null 后读取为 null
    /// </summary>
    [Fact]
    public void Current_AfterSetToNull_ReturnsNull()
    {
        var accessor = new TemplateContextAccessor
        {
            Current = new TemplateContext()
        };

        accessor.Current = null;

        Assert.Null(accessor.Current);
    }

    /// <summary>
    /// 子任务内的赋值不会回流到调用方
    /// </summary>
    [Fact]
    public async Task Current_SetInsideChildTask_DoesNotFlowBackToCaller()
    {
        var accessor = new TemplateContextAccessor
        {
            Current = null
        };

        await Task.Run(() => accessor.Current = new TemplateContext(), TestContext.Current.CancellationToken);

        // 异步局部值只向下游流动，子任务里挂上的上下文不能污染父流程
        Assert.Null(accessor.Current);
    }

    /// <summary>
    /// 调用方设置的上下文会流入子任务
    /// </summary>
    [Fact]
    public async Task Current_SetBeforeChildTask_FlowsIntoChildTask()
    {
        var accessor = new TemplateContextAccessor();
        var context = new TemplateContext();

        try
        {
            accessor.Current = context;

            var observed = await Task.Run(() => accessor.Current, TestContext.Current.CancellationToken);

            Assert.Same(context, observed);
        }
        finally
        {
            accessor.Current = null;
        }
    }
}
