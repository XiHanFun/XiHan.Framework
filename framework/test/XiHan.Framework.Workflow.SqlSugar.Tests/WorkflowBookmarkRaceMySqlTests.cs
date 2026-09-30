// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Exceptions;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Abstractions.Stores;
using XiHan.Framework.Workflow.Builders;
using XiHan.Framework.Workflow.SqlSugar.Stores;

namespace XiHan.Framework.Workflow.SqlSugar.Tests;

/// <summary>
/// 两个节点在没有跨进程锁时竞争同一书签的真实数据库测试
/// </summary>
/// <remarks>
/// 地址取环境变量 <c>XIHAN_TEST_MYSQL</c>，未设置时跳过。
/// 两个主机各自一套连接与进程内锁，共用同一个库。
/// </remarks>
public class WorkflowBookmarkRaceMySqlTests
{
    private const string SkipReason = "未设置 XIHAN_TEST_MYSQL，跳过真实数据库测试。";

    private static readonly string? ConnectionString =
        Environment.GetEnvironmentVariable("XIHAN_TEST_MYSQL");

    /// <summary>
    /// 两个节点竞争同一到期书签时只有一个执行批次运行
    /// </summary>
    [Fact]
    public async Task 两个节点竞争同一到期书签时只有一个执行批次运行()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), SkipReason);

        var rendezvous = new DeleteRendezvous(parties: 2);
        using var nodeA = new WorkflowEngineTestHost(
            WorkflowTestDatabase.CreateMySql(ConnectionString!), services => UseRendezvous(services, rendezvous), workerId: 11);
        using var nodeB = new WorkflowEngineTestHost(
            WorkflowTestDatabase.CreateMySql(ConnectionString!), services => UseRendezvous(services, rendezvous), workerId: 12);

        var code = "race-" + Guid.NewGuid().ToString("N");
        WorkflowDefinition? definition = null;
        WorkflowInstance? instance = null;

        try
        {
            definition = await nodeA.PublishAsync(BuildDelayDefinition(code));
            instance = await nodeA.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = code });

            var timer = Assert.Single(await nodeA.BookmarkStore.GetByInstanceAsync(instance.Id));
            rendezvous.BookmarkId = timer.Id;
            nodeA.Clock.Advance(TimeSpan.FromSeconds(301));
            nodeB.Clock.Advance(TimeSpan.FromSeconds(301));

            var outcomes = await Task.WhenAll(
                TryResumeAsync(nodeA, timer.Id),
                TryResumeAsync(nodeB, timer.Id));

            Assert.Equal(1, outcomes.Count(resumed => resumed));
            Assert.Equal(WorkflowInstanceStatus.Completed, (await nodeA.ReloadAsync(instance.Id)).Status);

            var history = await nodeA.InstanceStore.GetNodeInstancesAsync(instance.Id);
            Assert.Single(history, item => item.NodeId == "end");
        }
        finally
        {
            rendezvous.BookmarkId = null;

            if (instance is not null)
            {
                await nodeA.BookmarkStore.DeleteByInstanceAsync(instance.Id);
                await nodeA.InstanceStore.DeleteAsync(instance.Id);
            }

            if (definition is not null)
            {
                await nodeA.DefinitionStore.DeleteAsync(definition.Id);
            }
            else
            {
                var drafts = await nodeA.DefinitionStore.GetListAsync(code: code);
                foreach (var draft in drafts)
                {
                    await nodeA.DefinitionStore.DeleteAsync(draft.Id);
                }
            }
        }
    }

    private static void UseRendezvous(IServiceCollection services, DeleteRendezvous rendezvous)
    {
        services.Replace(ServiceDescriptor.Scoped<IWorkflowBookmarkStore>(provider => new RendezvousBookmarkStore(
            new SqlSugarWorkflowBookmarkStore(provider.GetRequiredService<WorkflowSqlSugarExecutor>()),
            rendezvous)));
    }

    private static async Task<bool> TryResumeAsync(WorkflowEngineTestHost node, string bookmarkId)
    {
        try
        {
            await node.Engine.ResumeBookmarkAsync(bookmarkId);
            return true;
        }
        catch (WorkflowException)
        {
            return false;
        }
    }

    private static WorkflowDefinition BuildDelayDefinition(string code)
    {
        return WorkflowDefinitionBuilder.Create(code, "竞争流程")
            .AddStart()
            .AddDelay("wait", 300)
            .AddEnd()
            .AddTransition("start", "wait")
            .AddTransition("wait", "end")
            .Build();
    }
}
