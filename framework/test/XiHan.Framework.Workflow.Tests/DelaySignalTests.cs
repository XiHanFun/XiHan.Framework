// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Builders;

namespace XiHan.Framework.Workflow.Tests;

/// <summary>
/// 延时与信号测试
/// </summary>
public class DelaySignalTests : IDisposable
{
    private readonly WorkflowTestHost _host = new();

    /// <summary>
    /// 延时节点挂起产生定时书签，到期恢复后完成
    /// </summary>
    [Fact]
    public async Task 延时节点挂起并由书签恢复()
    {
        var definition = WorkflowDefinitionBuilder.Create("delay", "延时流程")
            .AddStart()
            .AddDelay("wait", 300)
            .AddEnd()
            .AddTransition("start", "wait")
            .AddTransition("wait", "end")
            .Build();
        await _host.PublishAsync(definition);

        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "delay" });
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);

        var bookmarks = await _host.BookmarkStore.GetByInstanceAsync(instance.Id);
        var timer = Assert.Single(bookmarks);
        Assert.Equal(WorkflowBookmarkKinds.Timer, timer.Kind);
        Assert.Equal(_host.Clock.Now.AddSeconds(300), timer.DueTime);

        // 时钟拨到期后按到期查询可见
        _host.Clock.Advance(TimeSpan.FromSeconds(301));
        var due = await _host.BookmarkStore.GetDueAsync(_host.Clock.Now, 10);
        Assert.Single(due);

        var resumed = await _host.Engine.ResumeBookmarkAsync(timer.Id);
        Assert.Equal(WorkflowInstanceStatus.Completed, resumed.Status);
    }

    /// <summary>
    /// 信号按名称与业务相关性定向恢复
    /// </summary>
    [Fact]
    public async Task 信号按相关性定向恢复()
    {
        var definition = WorkflowDefinitionBuilder.Create("signal", "信号流程")
            .AddStart()
            .AddNode("wait", WorkflowActivityTypes.WaitSignal, "等待支付", node => node
                .WithProperty("SignalName", "order-paid"))
            .AddEnd()
            .AddTransition("start", "wait")
            .AddTransition("wait", "end")
            .Build();
        await _host.PublishAsync(definition);

        var first = await _host.Engine.StartAsync(new WorkflowStartRequest
        {
            DefinitionCode = "signal",
            CorrelationId = "ORDER-1"
        });
        var second = await _host.Engine.StartAsync(new WorkflowStartRequest
        {
            DefinitionCode = "signal",
            CorrelationId = "ORDER-2"
        });

        // 定向 ORDER-1：只恢复第一个实例
        var resumedCount = await _host.Engine.PublishSignalAsync(
            "order-paid", new Dictionary<string, object?> { ["paidAmount"] = 88 }, "ORDER-1");

        Assert.Equal(1, resumedCount);
        var firstReloaded = await _host.ReloadAsync(first.Id);
        var secondReloaded = await _host.ReloadAsync(second.Id);
        Assert.Equal(WorkflowInstanceStatus.Completed, firstReloaded.Status);
        Assert.Equal(WorkflowInstanceStatus.Running, secondReloaded.Status);
        Assert.Equal(88m, new WorkflowVariables(firstReloaded.Variables).Get<decimal>("paidAmount"));

        // 广播：恢复剩余实例
        Assert.Equal(1, await _host.Engine.PublishSignalAsync("order-paid"));
        Assert.Equal(WorkflowInstanceStatus.Completed, (await _host.ReloadAsync(second.Id)).Status);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _host.Dispose();
    }
}
