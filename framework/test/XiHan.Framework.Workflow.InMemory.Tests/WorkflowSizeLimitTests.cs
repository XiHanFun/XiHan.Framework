// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
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
/// 扇出与变量大小的上限（ADR-0182）
/// </summary>
/// <remarks>
/// 流程定义由租户用户编辑：ForEach 的项数、实例变量序列化后的大小与字符串拼接的长度都有上限，超过即故障或校验错误，不让一个定义耗尽宿主内存；级联取消覆盖全部子实例
/// </remarks>
public sealed class WorkflowSizeLimitTests
{
    /// <summary>
    /// 实例变量的缺省上限（字节）
    /// </summary>
    private const int VariablesLimit = 8 * 1024 * 1024;

    [Fact]
    public async Task ForEachOverTheItemLimit_FaultsWithoutStartingChildren()
    {
        await using var host = await StartHostAsync();
        await PublishAsync(host, Child("noop"));
        await PublishAsync(host, ForEachParent("fan-out", "noop", parallel: true));

        var started = await StartAsync(host, "fan-out", new JsonObject { ["items"] = Numbers(1001) });

        var instance = started.ValueOrThrow();
        Assert.Equal(WorkflowInstanceStatus.Faulted, instance.Status);
        Assert.Equal((WorkflowErrors.InputInvalid, WorkflowMessages.PropertyTooManyItems), (instance.Fault?.Code, instance.Fault?.MessageKey));
        Assert.Equal(0, await CountChildrenAsync(host, instance.Id, open: false));
    }

    [Fact]
    public async Task StartVariables_OverTheLimit_AreRejected()
    {
        await using var host = await StartHostAsync();
        await PublishAsync(host, Child("plain"));

        var started = await StartAsync(host, "plain", new JsonObject { ["s"] = new string('a', VariablesLimit + 1) });

        Assert.Equal((WorkflowErrors.InputInvalid, WorkflowMessages.VariablesTooLarge), (started.Error?.Code, started.Error?.MessageKey));
    }

    [Fact]
    public async Task NodeOutputs_OverTheLimit_FaultTheNode_AndLeaveTheVariablesAsTheyWere()
    {
        await using var host = await StartHostAsync();
        await PublishAsync(host, WorkflowDefinitionBuilder.Create(new WorkflowName("grow"))
            .AddStart()
            .AddNode("copy", new ActivityType(ActivityTypes.SetVariable), configure: static node => node.WithProperty(SetVariableActivity.ExpressionsProperty, new JsonObject { ["t"] = "s", ["u"] = "s" }))
            .AddEnd()
            .AddTransition("start", "copy").AddTransition("copy", "end")
            .Build());

        var instance = (await StartAsync(host, "grow", new JsonObject { ["s"] = new string('a', 3 * 1024 * 1024) })).ValueOrThrow();

        Assert.Equal(WorkflowInstanceStatus.Faulted, instance.Status);
        Assert.Equal(WorkflowMessages.VariablesTooLarge, instance.Fault?.MessageKey);
        Assert.Equal(["s"], instance.Variables.Select(static pair => pair.Key));
    }

    [Fact]
    public async Task StringConcatenation_OverTheLimit_IsAnExpressionError()
    {
        await using var host = await StartHostAsync();
        await PublishAsync(host, WorkflowDefinitionBuilder.Create(new WorkflowName("concat"))
            .AddStart()
            .AddNode("join", new ActivityType(ActivityTypes.SetVariable), configure: static node => node.WithProperty(SetVariableActivity.ExpressionsProperty, new JsonObject { ["t"] = "s + s + s + s + s" }))
            .AddEnd()
            .AddTransition("start", "join").AddTransition("join", "end")
            .Build());

        var instance = (await StartAsync(host, "concat", new JsonObject { ["s"] = new string('a', 1024 * 1024) })).ValueOrThrow();

        Assert.Equal(WorkflowInstanceStatus.Faulted, instance.Status);
        Assert.Equal(WorkflowMessages.ExpressionStringTooLong, instance.Fault?.MessageKey);
        Assert.False(instance.Variables.ContainsKey("t"));
    }

    [Fact]
    public async Task ForEachResults_OverTheVariableLimit_FaultTheNode()
    {
        await using var host = await StartHostAsync();
        await PublishAsync(host, Child("echo"));
        await PublishAsync(host, ForEachParent("collect", "echo", parallel: false, resultVariable: "results", childVariables: new JsonObject { ["pad"] = new string('a', 1024 * 1024) }));

        var started = (await StartAsync(host, "collect", new JsonObject { ["items"] = Numbers(10) })).ValueOrThrow();
        var instance = await FinalAsync(host, started.Id);

        Assert.Equal(WorkflowInstanceStatus.Faulted, instance.Status);
        Assert.Equal(WorkflowMessages.VariablesTooLarge, instance.Fault?.MessageKey);
        Assert.False(instance.Variables.ContainsKey("results"));
    }

    [Fact]
    public async Task CancellingTheParent_CancelsEveryOpenChild_BeyondOnePage()
    {
        await using var host = await StartHostAsync(new KeyValuePair<string, string?>($"{WorkflowOptions.ConfigurationSection}:MaxForEachItems", "1500"));
        await PublishAsync(host, WorkflowDefinitionBuilder.Create(new WorkflowName("wait"))
            .AddStart()
            .AddNode("wait", new ActivityType(ActivityTypes.WaitSignal), configure: static node => node.WithProperty(WaitSignalActivity.SignalNameProperty, "never").WithProperty(WaitSignalActivity.AcceptAnyCorrelationProperty, true))
            .AddEnd()
            .AddTransition("start", "wait").AddTransition("wait", "end")
            .Build());
        await PublishAsync(host, ForEachParent("wide", "wait", parallel: true));
        var parent = (await StartAsync(host, "wide", new JsonObject { ["items"] = Numbers(1500) })).ValueOrThrow();
        Assert.Equal(1500, await CountChildrenAsync(host, parent.Id, open: true));

        await using (var scope = host.Services.CreateAsyncScope())
        {
            (await scope.ServiceProvider.GetRequiredService<IWorkflowEngine>().CancelAsync(parent.Id, "stop", CancellationToken.None)).ValueOrThrow();
        }

        Assert.Equal(0, await CountChildrenAsync(host, parent.Id, open: true));
    }

    /// <summary>
    /// 起一个内存工作流宿主
    /// </summary>
    private static Task<TestHost> StartHostAsync(params KeyValuePair<string, string?>[] configuration)
        => TestHost.StartAsync(
            static b => b.UseFeature<InMemoryWorkflowFeature>(),
            [new($"{InMemoryWorkflowOptions.ConfigurationSection}:PollIntervalSeconds", "3600"), .. configuration]);

    /// <summary>
    /// 开始即结束的子流程
    /// </summary>
    private static WorkflowDefinition Child(string name)
        => WorkflowDefinitionBuilder.Create(new WorkflowName(name)).AddStart().AddEnd().AddTransition("start", "end").Build();

    /// <summary>
    /// 对 <c>items</c> 逐项启动 <paramref name="child"/> 的父流程
    /// </summary>
    private static WorkflowDefinition ForEachParent(string name, string child, bool parallel, string? resultVariable = null, JsonObject? childVariables = null)
        => WorkflowDefinitionBuilder.Create(new WorkflowName(name))
            .AddStart()
            .AddNode("each", new ActivityType(ActivityTypes.ForEach), configure: node =>
            {
                node.WithProperty(ForEachActivity.ItemsProperty, "items")
                    .WithProperty(SubWorkflowActivity.DefinitionNameProperty, child)
                    .WithProperty(ForEachActivity.ParallelProperty, parallel);
                if (resultVariable is not null)
                {
                    node.WithProperty(SubWorkflowActivity.ResultVariableProperty, resultVariable);
                }

                if (childVariables is not null)
                {
                    node.WithProperty(SubWorkflowActivity.VariablesProperty, childVariables);
                }
            })
            .AddEnd()
            .AddTransition("start", "each").AddTransition("each", "end")
            .Build();

    /// <summary>
    /// 0 到 <paramref name="count"/> - 1 的数组
    /// </summary>
    private static JsonArray Numbers(int count) => new([.. Enumerable.Range(0, count).Select(static i => (JsonNode?)i)]);

    /// <summary>
    /// 建并发布定义
    /// </summary>
    private static async Task PublishAsync(TestHost host, WorkflowDefinition definition)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IWorkflowDefinitionCatalog>();
        var created = (await catalog.CreateAsync(definition, CancellationToken.None)).ValueOrThrow();
        (await catalog.PublishAsync(created.Id, CancellationToken.None)).ValueOrThrow();
    }

    /// <summary>
    /// 以给定变量启动
    /// </summary>
    private static async Task<Result<WorkflowInstance>> StartAsync(TestHost host, string name, JsonObject variables)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IWorkflowEngine>().StartAsync(new WorkflowStartRequest { Name = new WorkflowName(name), Variables = variables }, CancellationToken.None);
    }

    /// <summary>
    /// 等实例结束并返回它
    /// </summary>
    private static async Task<WorkflowInstance> FinalAsync(TestHost host, string id)
    {
        for (var attempt = 0; ; attempt++)
        {
            await using var scope = host.Services.CreateAsyncScope();
            var instance = (await scope.ServiceProvider.GetRequiredService<IWorkflowEngine>().FindInstanceAsync(id, CancellationToken.None)).ValueOrThrow().ValueOrThrow();
            if (instance.IsFinal || attempt == 200)
            {
                return instance;
            }

            await Task.Delay(50);
        }
    }

    /// <summary>
    /// 子实例数，<paramref name="open"/> 为真时只数没结束的
    /// </summary>
    private static async Task<long> CountChildrenAsync(TestHost host, string parentId, bool open)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var query = new WorkflowInstanceQuery
        {
            ParentInstanceId = parentId,
            Statuses = open ? [WorkflowInstanceStatus.Running, WorkflowInstanceStatus.Suspended] : null,
        };
        return (await scope.ServiceProvider.GetRequiredService<IWorkflowEngine>().CountInstancesAsync(query, CancellationToken.None)).ValueOrThrow();
    }
}
