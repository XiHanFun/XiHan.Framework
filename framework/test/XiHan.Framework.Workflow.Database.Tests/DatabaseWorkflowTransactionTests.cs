// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.MultiTenancy.InMemory;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Tasks;
using XiHan.Framework.Workflow.Conformance.Engine;
using XiHan.Framework.Workflow.Database.Entities;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 数据库实现与业务同事务、随租户分库（ADR-0077）
/// </summary>
/// <remarks>
/// 结果处理器的业务写入与实例完成一起提交或回滚；租户的记录落在它自己的库里，平台的落平台表；轮询器经 Tenant 阶段绑定分片，覆盖租户库里的到期书签
/// </remarks>
public sealed class DatabaseWorkflowTransactionTests
{
    [Fact]
    public async Task OutcomeHandlerWrites_AndInstanceCompletion_CommitOrRollBackTogether()
    {
        var database = new SqliteDatabase();
        var host = await database.StartHostAsync(services: static services =>
        {
            services.AddRepository<ApprovalGrant, string>();
            services.AddWorkflowOutcomeHandler<GrantingOutcomeHandler>();
        });
        await using var workflow = new WorkflowEngineHost(host.Services, static _ => ValueTask.FromResult(0), database);
        await workflow.PublishAsync(WorkflowDefinitionBuilder.Create("grant")
            .AddStart()
            .AddUserTask("approve", "Approve", static node => node.WithAssigneeRule("User", ["alice"]))
            .AddEnd("approved", outcome: UserTaskOutcomes.Approved)
            .AddTransition("start", "approve").AddTransition("approve", "approved")
            .Build());
        var instance = (await workflow.StartAsync("grant")).ValueOrThrow();
        var task = Assert.Single(await workflow.PendingAsync("alice"));

        // 办理到结论、处理器写了授予，业务随后失败：授予与实例完成一起回滚，任务仍在待办
        await workflow.AsTenantAsync(null, async sp =>
        {
            var completed = (await sp.GetRequiredService<IWorkflowTaskService>().CompleteAsync(new UserTaskCompletion(task.Id, "alice")
            {
                Outcome = UserTaskOutcomes.Approved,
            }, CancellationToken.None)).ValueOrThrow();
            Assert.Equal(WorkflowInstanceStatus.Completed, completed.Status);
            Assert.Equal(1, await GrantsAsync(sp));
            return true;
        }, rollback: true);
        Assert.Equal(0, await workflow.AsTenantAsync(null, GrantsAsync));
        Assert.Equal(WorkflowInstanceStatus.Running, (await workflow.InstanceAsync(instance.Id)).Status);
        Assert.Equal([task.Id], (await workflow.PendingAsync("alice")).Select(static pending => pending.Id));

        // 提交：授予与实例完成一起落库
        await workflow.AsTenantAsync(null, async sp => (await sp.GetRequiredService<IWorkflowTaskService>().CompleteAsync(new UserTaskCompletion(task.Id, "alice")
        {
            Outcome = UserTaskOutcomes.Approved,
        }, CancellationToken.None)).ValueOrThrow());
        Assert.Equal(1, await workflow.AsTenantAsync(null, GrantsAsync));
        Assert.Equal(WorkflowInstanceStatus.Completed, (await workflow.InstanceAsync(instance.Id)).Status);
    }

    [Fact]
    public async Task TenantRecords_LandInTheTenantsOwnDatabase_AndThePollerCoversIt()
    {
        var database = new SqliteDatabase();
        var shardPath = database.Path + ".t-shard.db";
        database.ExtraFiles.Add(shardPath);
        // 分片库的表结构由应用管理（框架的建表只作用于默认连接）：测试自己建
        using (var shard = new SqlSugarClient(SqliteDatabase.Config("shard-init", SqliteDatabase.Connection(shardPath))))
        {
            shard.CodeFirst.InitTables(typeof(WorkflowDefinitionRecord), typeof(WorkflowInstanceRecord), typeof(WorkflowNodeInstanceRecord), typeof(WorkflowBookmarkRecord));
        }

        var host = await database.StartHostAsync(
            [
                new("XiHan:MultiTenancy:Tenants:t-shard:Properties:ConnectionString", SqliteDatabase.Connection(shardPath)),
                new("XiHan:MultiTenancy:Tenants:t-shared:DisplayName", "Shared"),
            ],
            configure: static b => b.UseFeature<InMemoryMultiTenancyFeature>());
        await using (database)
        {
            var definition = WorkflowDefinitionBuilder.Create("sharded").AddStart().AddDelay("wait", TimeSpan.FromMilliseconds(1)).AddEnd()
                .AddTransition("start", "wait").AddTransition("wait", "end").Build();
            var started = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var tenant in new[] { "t-shard", "t-shared" })
            {
                // 经 Baggage 带租户：Tenant 阶段解析、校验并绑定分片，工作单元按这个租户开启
                started[tenant] = await AsTenantAsync(host, tenant, async sp =>
                {
                    var catalog = sp.GetRequiredService<IWorkflowDefinitionCatalog>();
                    var created = (await catalog.CreateAsync(definition, CancellationToken.None)).ValueOrThrow();
                    (await catalog.PublishAsync(created.Id, CancellationToken.None)).ValueOrThrow();
                    return (await sp.GetRequiredService<IWorkflowEngine>().StartAsync(new WorkflowStartRequest { Name = "sharded" }, CancellationToken.None)).ValueOrThrow().Id;
                });
            }

            // 独立库的租户：行在它自己的库里；共库的租户：行在默认库的租户表里；平台表一行也没有
            using (var shard = new SqlSugarClient(SqliteDatabase.Config("shard-read", SqliteDatabase.Connection(shardPath))))
            using (var main = new SqlSugarClient(SqliteDatabase.Config("main-read", database.ConnectionString)))
            {
                Assert.Equal(["t-shard"], shard.Queryable<WorkflowInstanceRecord>().Select(static row => row.TenantId).ToList());
                Assert.Equal(["t-shard"], shard.Queryable<WorkflowDefinitionRecord>().Select(static row => row.TenantId).ToList());
                Assert.Equal(["t-shared"], main.Queryable<WorkflowInstanceRecord>().Select(static row => row.TenantId).ToList());
                Assert.Equal(0, main.Queryable<PlatformWorkflowInstanceRecord>().Count());
            }

            // 轮询器从租户目录取租户、以租户开调用：分片库里的到期书签同样被恢复
            await Task.Delay(20);
            var resumed = await host.Services.GetRequiredService<DueBookmarkPoller>().PollOnceAsync(100, CancellationToken.None);
            Assert.Equal(2, resumed);
            foreach (var (tenant, id) in started)
            {
                Assert.Equal(WorkflowInstanceStatus.Completed, await AsTenantAsync(host, tenant, async sp => (await sp.GetRequiredService<IWorkflowEngine>().FindInstanceAsync(id, CancellationToken.None)).ValueOrThrow().ValueOrThrow().Status));
            }
        }
    }

    /// <summary>
    /// 授予的行数
    /// </summary>
    private static async ValueTask<int> GrantsAsync(IServiceProvider services)
        => (int)await services.GetRequiredService<IRepository<ApprovalGrant, string>>().CountAsync(EntityQuery.All<ApprovalGrant>(), CancellationToken.None);

    /// <summary>
    /// 以 Baggage 带租户开一次调用执行 <paramref name="action"/>，返回后提交
    /// </summary>
    private static async Task<T> AsTenantAsync<T>(TestHost host, string tenantId, Func<IServiceProvider, Task<T>> action)
    {
        var outcome = default(T);
        var invoked = await host.InvokeAsync(async context => outcome = await action(context.Services), baggage: new Dictionary<string, string>(StringComparer.Ordinal) { [BaggageKeys.Tenant] = tenantId });
        invoked.ThrowIfFailure();
        return outcome!;
    }
}
