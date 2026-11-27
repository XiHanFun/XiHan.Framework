// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

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
/// 发起人不含孤立的代理项
/// </summary>
/// <remarks>
/// RFC-0021「实例与执行」（ADR-0334）：发起人写进实例、待办与事件的 <c>starterId</c>，是「我发起的」的查询条件与 <c>Starter</c> 指派规则解析出的处理人；只差在孤立代理项上的两个发起人宽松编码之后相同，所以启动与查询的入口都拒收
/// </remarks>
public sealed class WorkflowStarterTests
{
    /// <summary>
    /// 启动即等信号的定义名：实例停在等待处
    /// </summary>
    private const string Waiting = "waits";

    [Fact]
    public async Task StartsWithALoneSurrogateInTheStarter_AreRejected_BeforeAnInstanceIsWritten()
    {
        await using var host = await StartHostAsync();
        await PublishWaitingAsync(host);

        // 改之前都启动成功，发起人原样进了存储与事件
        foreach (var starter in new[] { "alice\uD800", "alice\uDBFF", "\uDC00alice", "\uDE00\uD83D" })
        {
            var started = await StartAsync(host, new WorkflowStartRequest { Name = new WorkflowName(Waiting), StarterId = starter });
            Assert.Equal((WorkflowErrors.InputInvalid, WorkflowMessages.InstanceStarterInvalid), (started.Error?.Code, started.Error?.MessageKey));
            Assert.True(started.Error!.Details is null or { Count: 0 });
        }

        Assert.Equal(0, await CountAsync(host, new WorkflowInstanceQuery()));
    }

    [Fact]
    public async Task TheCorrelationId_IsCheckedBeforeTheStarter()
    {
        await using var host = await StartHostAsync();

        var started = await StartAsync(host, new WorkflowStartRequest { Name = new WorkflowName(Waiting), CorrelationId = "order\uD800", StarterId = "alice\uD800" });

        Assert.Equal(WorkflowMessages.InstanceCorrelationInvalid, started.Error?.MessageKey);
    }

    [Fact]
    public async Task ASurrogatePair_IsAValidStarter_AndQueriesFindIt()
    {
        await using var host = await StartHostAsync();
        await PublishWaitingAsync(host);

        var started = (await StartAsync(host, new WorkflowStartRequest { Name = new WorkflowName(Waiting), StarterId = "alice-😀" })).ValueOrThrow();

        Assert.Equal("alice-😀", started.StarterId);
        Assert.Equal(1, await CountAsync(host, new WorkflowInstanceQuery { StarterId = "alice-😀" }));
    }

    [Fact]
    public async Task QueriesByAStarterWithALoneSurrogate_AreRejected()
    {
        await using var host = await StartHostAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var engine = scope.ServiceProvider.GetRequiredService<IWorkflowEngine>();
        var query = new WorkflowInstanceQuery { StarterId = "alice\uD800" };

        // 修之前照查：内存实现按码元比较、查不到，数据库实现经参数宽松编码可能查到另一个发起人的实例
        var listed = await engine.ListInstancesAsync(query, CancellationToken.None);
        var counted = await engine.CountInstancesAsync(query, CancellationToken.None);

        Assert.Equal((WorkflowErrors.InputInvalid, WorkflowMessages.InstanceStarterInvalid), (listed.Error?.Code, listed.Error?.MessageKey));
        Assert.Equal((WorkflowErrors.InputInvalid, WorkflowMessages.InstanceStarterInvalid), (counted.Error?.Code, counted.Error?.MessageKey));
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
    /// 在新的作用域里启动
    /// </summary>
    private static async Task<Result<WorkflowInstance>> StartAsync(TestHost host, WorkflowStartRequest request)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IWorkflowEngine>().StartAsync(request, CancellationToken.None);
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
