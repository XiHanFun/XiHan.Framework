// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Assignees;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Tasks;
using XiHan.Framework.Workflow.Conformance.Engine;

namespace XiHan.Framework.Workflow.InMemory.Tests;

/// <summary>
/// 内存工作流在只读调用里拒绝写入（ADR-0099）
/// </summary>
/// <remarks>
/// 宿主只装内存工作流、不装持久化：没有 Transaction 阶段、没有工作单元，判定只看调用的操作契约。装了持久化与发件箱的宿主由一致性套件用例 30 覆盖（与数据库实现同一断言）
/// </remarks>
public sealed class ReadOnlyInvocationTests
{
    [Fact]
    public async Task ReadOnlyInvocation_WithoutPersistence_RejectsEveryWriteBeforeRunning_AndReadsStillWork()
    {
        await using var host = await StartAsync();
        await host.PublishAsync(Approval());
        var running = (await host.StartAsync(new WorkflowName("ro-approval"))).ValueOrThrow();
        var task = Assert.Single(await host.PendingAsync("alice"));

        await host.AsTenantAsync(null, async services =>
        {
            var engine = services.GetRequiredService<IWorkflowEngine>();
            var tasks = services.GetRequiredService<IWorkflowTaskService>();
            var catalog = services.GetRequiredService<IWorkflowDefinitionCatalog>();

            // 引擎、任务服务、定义目录的写入都在取锁、执行活动之前拒绝，错误码与消息键同数据库实现
            AssertRejected((await engine.StartAsync(new WorkflowStartRequest { Name = new WorkflowName("ro-approval") }, CancellationToken.None)).Error);
            AssertRejected((await engine.SuspendAsync(running.Id, "hold", CancellationToken.None)).Error);
            AssertRejected((await tasks.CompleteAsync(new UserTaskCompletion(task.Id, "alice")
            {
                Outcome = UserTaskOutcomes.Approved,
            }, CancellationToken.None)).Error);
            AssertRejected((await catalog.CreateAsync(Approval() with { Name = new WorkflowName("ro-other") }, CancellationToken.None)).Error);

            // 查询照常
            Assert.True((await engine.FindInstanceAsync(running.Id, CancellationToken.None)).ValueOrThrow().HasValue);
            Assert.Equal(1, (await tasks.QueryAsync(new UserTaskQuery { AssigneeId = "alice" }, CancellationToken.None)).ValueOrThrow().Total);
            Assert.Equal(1, (await catalog.QueryAsync(new WorkflowDefinitionQuery(), CancellationToken.None)).ValueOrThrow().Total);
            return true;
        }, readOnly: true);

        // 什么也没写下：实例仍只有一个、仍在运行，任务仍待办，没有新定义
        Assert.Single(await host.ListInstancesAsync(new WorkflowInstanceQuery()));
        Assert.Equal(WorkflowInstanceStatus.Running, (await host.InstanceAsync(running.Id)).Status);
        Assert.Equal(task.Id, Assert.Single(await host.PendingAsync("alice")).Id);
        await host.AsTenantAsync(null, async services =>
        {
            Assert.Equal(1, (await services.GetRequiredService<IWorkflowDefinitionCatalog>().QueryAsync(new WorkflowDefinitionQuery(), CancellationToken.None)).ValueOrThrow().Total);
            return true;
        });

        // 可写的调用照常办理
        Assert.Equal(WorkflowInstanceStatus.Completed, (await host.CompleteAsync(task.Id, "alice", UserTaskOutcomes.Approved)).ValueOrThrow().Status);
    }

    [Fact]
    public async Task Feature_RegistersThePersistenceCatalog_SoTheRejectionTranslatesWithoutAPersistenceProvider()
    {
        await using var host = await StartAsync();
        var catalogs = host.Services.GetServices<MessageCatalog>().Select(static catalog => catalog.Name).ToList();
        Assert.Contains(PersistenceMessages.CatalogName, catalogs);
        Assert.Single(catalogs, static name => name == PersistenceMessages.CatalogName);
        var persistence = host.Services.GetServices<MessageCatalog>().Single(static catalog => catalog.Name == PersistenceMessages.CatalogName);
        Assert.True(persistence.Find(PersistenceMessages.ReadOnly, "en-US").HasValue);
    }

    /// <summary>
    /// 断言是只读拒绝
    /// </summary>
    private static void AssertRejected(Error? error)
        => Assert.Equal((PersistenceErrors.ReadOnly, PersistenceMessages.ReadOnly), (error?.Code, error?.MessageKey));

    /// <summary>
    /// 只装内存工作流的宿主（不装持久化），轮询周期拉长、由用例主动轮询
    /// </summary>
    private static async Task<WorkflowEngineHost> StartAsync()
    {
        var host = await TestHost.StartAsync(static b => b.UseFeature<InMemoryWorkflowFeature>(), [new($"{InMemoryWorkflowOptions.ConfigurationSection}:PollIntervalSeconds", "3600")]);
        var poller = host.Services.GetRequiredService<DueBookmarkPoller>();
        return new WorkflowEngineHost(host.Services, ct => poller.PollOnceAsync(100, ct), host);
    }

    /// <summary>
    /// 开始 → 指派给 alice 的用户任务 → 结束
    /// </summary>
    private static WorkflowDefinition Approval()
        => WorkflowDefinitionBuilder.Create(new WorkflowName("ro-approval"))
            .AddStart()
            .AddUserTask("approve", "Approve", static node => node.WithAssigneeRule(AssigneeRuleTypes.User, "alice"))
            .AddEnd()
            .AddTransition("start", "approve").AddTransition("approve", "end")
            .Build();
}
