// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;

namespace XiHan.Framework.Threading.Tests;

/// <summary>
/// 异步本地环境数据上下文测试
/// </summary>
/// <remarks>
/// 该类型的核心契约是 AsyncLocal 的传播语义：值沿异步流向下传播到子任务，不向上回灌到父流，
/// 并行分支之间互相隔离。键槽由静态字典持有，隔离维度是异步流而不是上下文实例。
/// 所有用例都用随机键，避免用例之间经由静态字典互相干扰。
/// </remarks>
public class AsyncLocalAmbientDataContextTests
{
    /// <summary>
    /// 写入后可按同一键读回
    /// </summary>
    [Fact]
    public void SetData_ThenGetData_ReturnsValue()
    {
        var context = new AsyncLocalAmbientDataContext();
        var key = NewKey();

        context.SetData(key, "值");

        Assert.Equal("值", context.GetData(key) as string);
    }

    /// <summary>
    /// 从未写入过的键读出空值，且不抛异常
    /// </summary>
    [Fact]
    public void GetData_ForUnknownKey_ReturnsNull()
    {
        var context = new AsyncLocalAmbientDataContext();

        Assert.Null(context.GetData(NewKey()));
    }

    /// <summary>
    /// 写入空值等价于清空该键
    /// </summary>
    [Fact]
    public void SetData_WithNull_ClearsValue()
    {
        var context = new AsyncLocalAmbientDataContext();
        var key = NewKey();

        context.SetData(key, "值");
        context.SetData(key, null);

        Assert.Null(context.GetData(key));
    }

    /// <summary>
    /// 同一键重复写入以最后一次为准
    /// </summary>
    [Fact]
    public void SetData_Twice_KeepsLatestValue()
    {
        var context = new AsyncLocalAmbientDataContext();
        var key = NewKey();

        context.SetData(key, "旧值");
        context.SetData(key, "新值");

        Assert.Equal("新值", context.GetData(key) as string);
    }

    /// <summary>
    /// 不同键彼此独立，互不覆盖
    /// </summary>
    [Fact]
    public void SetData_WithDifferentKeys_AreIndependent()
    {
        var context = new AsyncLocalAmbientDataContext();
        var firstKey = NewKey();
        var secondKey = NewKey();

        context.SetData(firstKey, "第一个");
        context.SetData(secondKey, "第二个");

        Assert.Equal("第一个", context.GetData(firstKey) as string);
        Assert.Equal("第二个", context.GetData(secondKey) as string);
    }

    /// <summary>
    /// 值沿异步流向下传播到子任务
    /// </summary>
    [Fact]
    public async Task SetData_FlowsIntoChildTask()
    {
        var context = new AsyncLocalAmbientDataContext();
        var key = NewKey();
        context.SetData(key, "外层");

        var observed = await Task.Run(() => context.GetData(key) as string, TestContext.Current.CancellationToken);

        Assert.Equal("外层", observed);
    }

    /// <summary>
    /// 子任务内的写入不回灌到父流
    /// </summary>
    /// <remarks>
    /// 这是 AsyncLocal 与普通静态字段的分水岭：子流的修改被限制在子流的执行上下文副本里。
    /// </remarks>
    [Fact]
    public async Task SetData_InsideChildTask_DoesNotFlowBackToCaller()
    {
        var context = new AsyncLocalAmbientDataContext();
        var key = NewKey();

        await Task.Run(() => context.SetData(key, "内层"), TestContext.Current.CancellationToken);

        Assert.Null(context.GetData(key));
    }

    /// <summary>
    /// 值跨 await 之后在续体里继续可见
    /// </summary>
    [Fact]
    public async Task SetData_AcrossAwait_SurvivesContinuation()
    {
        var context = new AsyncLocalAmbientDataContext();
        var key = NewKey();

        context.SetData(key, "跨越等待");
        await Task.Yield();

        Assert.Equal("跨越等待", context.GetData(key) as string);
    }

    /// <summary>
    /// 并行分支各自持有独立的值，互不串写
    /// </summary>
    [Fact]
    public async Task SetData_InParallelTasks_IsIsolatedPerBranch()
    {
        var context = new AsyncLocalAmbientDataContext();
        var key = NewKey();

        var observed = await Task.WhenAll(Enumerable.Range(0, 8).Select(index => Task.Run(async () =>
        {
            context.SetData(key, $"分支-{index}");
            await Task.Yield();
            return context.GetData(key) as string;
        }, TestContext.Current.CancellationToken)));

        for (var index = 0; index < observed.Length; index++)
        {
            Assert.Equal($"分支-{index}", observed[index]);
        }

        Assert.Null(context.GetData(key));
    }

    /// <summary>
    /// 同一键在不同实例之间共享同一个异步本地槽
    /// </summary>
    /// <remarks>
    /// 键槽存放在静态字典里，这是该实现刻意选择的进程级键空间；
    /// 换言之隔离维度是异步流，新建一个上下文实例并不会得到一份新的数据。
    /// </remarks>
    [Fact]
    public void GetData_FromAnotherInstance_SeesSameKeySlot()
    {
        var key = NewKey();
        var writer = new AsyncLocalAmbientDataContext();
        var reader = new AsyncLocalAmbientDataContext();

        writer.SetData(key, "共享");

        Assert.Equal("共享", reader.GetData(key) as string);
    }

    /// <summary>
    /// 实现环境数据上下文契约，并按单例生命周期约定登记
    /// </summary>
    [Fact]
    public void Type_ImplementsContractAndSingletonConvention()
    {
        var context = new AsyncLocalAmbientDataContext();

        Assert.IsAssignableFrom<IAmbientDataContext>(context);
        Assert.IsAssignableFrom<ISingletonDependency>(context);
    }

    /// <summary>
    /// 生成一个仅本用例使用的随机上下文键
    /// </summary>
    private static string NewKey()
    {
        return "XiHan.Framework.Threading.Tests." + Guid.NewGuid().ToString("N");
    }
}
