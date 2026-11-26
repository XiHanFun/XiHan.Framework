// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Instances;

namespace XiHan.Framework.Workflow.InMemory.Tests;

/// <summary>
/// 关联 ID 不含孤立的代理项
/// </summary>
/// <remarks>
/// RFC-0021「实例与执行」（ADR-0321）：查重的锁键按 UTF-8 散列关联 ID，它又是事件的扩展属性 <c>correlation</c>；只差在孤立代理项上的两个关联 ID 宽松编码之后相同，所以启动、信号与查询的入口都拒收
/// </remarks>
public sealed class WorkflowCorrelationTests
{
    /// <summary>
    /// 启动即等信号的定义名：实例停在等待处，查重看得见它
    /// </summary>
    private const string Waiting = "waits";

    [Fact]
    public async Task StartsWithCorrelationIdsThatDifferOnlyInALoneSurrogate_AreRejected_BeforeAnInstanceIsWritten()
    {
        await using var host = await StartHostAsync();
        await PublishWaitingAsync(host);

        // 改之前两个都启动成功：锁键是同一段宽松 UTF-8 的散列，事件的 correlation 写不出，这两个实例的事件都发不出去
        Assert.Equal(Encoding.UTF8.GetBytes("order-1\uD800"), Encoding.UTF8.GetBytes("order-1\uDBFF"));
        foreach (var correlation in new[] { "order-1\uD800", "order-1\uDBFF", "\uDC00order", "\uDE00\uD83D" })
        {
            var started = await StartAsync(host, correlation, WorkflowReusePolicy.RejectActive);
            Assert.Equal((WorkflowErrors.InputInvalid, WorkflowMessages.InstanceCorrelationInvalid), (started.Error?.Code, started.Error?.MessageKey));
        }

        Assert.Equal(0, await CountAsync(host, new WorkflowInstanceQuery()));
    }

    [Fact]
    public async Task ASurrogatePair_IsAValidCorrelationId_AndTheReusePolicyStillSeesIt()
    {
        await using var host = await StartHostAsync();
        await PublishWaitingAsync(host);

        var first = (await StartAsync(host, "order-😀", WorkflowReusePolicy.RejectActive)).ValueOrThrow();
        var second = await StartAsync(host, "order-😀", WorkflowReusePolicy.RejectActive);

        Assert.Equal("order-😀", first.CorrelationId);
        Assert.Equal(WorkflowErrors.ActiveInstanceExists, second.Error?.Code);
    }

    [Fact]
    public async Task SignalsWithALoneSurrogateInTheCorrelationId_AreRejected()
    {
        await using var host = await StartHostAsync();
        await PublishWaitingAsync(host);
        await using var scope = host.Services.CreateAsyncScope();
        var engine = scope.ServiceProvider.GetRequiredService<IWorkflowEngine>();

        var signaled = await engine.SignalAsync("go", null, "order-1\uD800", CancellationToken.None);

        Assert.Equal((WorkflowErrors.InputInvalid, WorkflowMessages.InstanceCorrelationInvalid), (signaled.Error?.Code, signaled.Error?.MessageKey));
    }

    [Fact]
    public async Task QueriesByCorrelationIdsWithALoneSurrogate_AreRejected_LikeBlankIds()
    {
        await using var host = await StartHostAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var engine = scope.ServiceProvider.GetRequiredService<IWorkflowEngine>();
        var query = new WorkflowInstanceQuery { CorrelationIds = ["order-1", "order-1\uD800"] };

        var listed = await engine.ListInstancesAsync(query, CancellationToken.None);
        var counted = await engine.CountInstancesAsync(query, CancellationToken.None);

        Assert.Equal((WorkflowErrors.InputInvalid, WorkflowMessages.InstanceQueryCorrelationsInvalid), (listed.Error?.Code, listed.Error?.MessageKey));
        Assert.Equal((WorkflowErrors.InputInvalid, WorkflowMessages.InstanceQueryCorrelationsInvalid), (counted.Error?.Code, counted.Error?.MessageKey));
    }

    /// <summary>
    /// 启动只装配内存工作流的宿主，轮询器不在用例里运行
    /// </summary>
    private static Task<TestHost> StartHostAsync()
        => TestHost.StartAsync(static b => b.UseFeature<InMemoryWorkflowFeature>(), [new($"{InMemoryWorkflowOptions.ConfigurationSection}:PollIntervalSeconds", "3600")]);

    /// <summary>
    /// 建并发布启动即等信号 <c>go</c> 的定义
    /// </summary>
    private static async Task PublishWaitingAsync(TestHost host)
    {
        var definition = WorkflowDefinitionBuilder.Create(new WorkflowName(Waiting))
            .AddStart()
            .AddNode("wait", new ActivityType(ActivityTypes.WaitSignal), configure: static node => node.WithProperty(WaitSignalActivity.SignalNameProperty, "go"))
            .AddEnd()
            .AddTransition("start", "wait").AddTransition("wait", "end")
            .Build();
        await using var scope = host.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IWorkflowDefinitionCatalog>();
        var created = (await catalog.CreateAsync(definition, CancellationToken.None)).ValueOrThrow();
        (await catalog.PublishAsync(created.Id, CancellationToken.None)).ValueOrThrow();
    }

    /// <summary>
    /// 以关联 ID 与重用策略启动等信号的定义
    /// </summary>
    private static async Task<Result<WorkflowInstance>> StartAsync(TestHost host, string correlation, WorkflowReusePolicy policy)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IWorkflowEngine>().StartAsync(new WorkflowStartRequest { Name = new WorkflowName(Waiting), CorrelationId = correlation, ReusePolicy = policy }, CancellationToken.None);
    }

    /// <summary>
    /// 按查询计数实例
    /// </summary>
    private static async Task<long> CountAsync(TestHost host, WorkflowInstanceQuery query)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<IWorkflowEngine>().CountInstancesAsync(query, CancellationToken.None)).ValueOrThrow();
    }
}
