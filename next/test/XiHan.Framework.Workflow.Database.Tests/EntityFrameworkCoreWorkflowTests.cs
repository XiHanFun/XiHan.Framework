// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Storage;
using XiHan.Framework.Workflow.Abstractions.Tasks;
using XiHan.Framework.Workflow.Conformance.Engine;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 数据库工作流在 EF Core（SQLite）上的租约表与业务事务。
/// </summary>
/// <remarks>
/// 租约表放在命名连接的另一个库文件（ADR-0087）。
/// 引擎在调用方事务里写过业务库之后取实例锁，租约行写在租约库上，不等调用方自己的事务；结果处理器的写入与实例完成一起提交或回滚。
/// </remarks>
public sealed class EntityFrameworkCoreWorkflowTests
{
    [Fact]
    public async Task LeaseRows_LandInTheLeaseDatabase_WhileTheCallersTransactionHoldsTheBusinessDatabase()
    {
        var database = new EntityFrameworkCoreSqliteDatabase();
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

        // 同一次调用里先写业务（授予表）、再发起流程：SQLite 的库级写锁在调用方事务手里，实例锁的租约行写在租约库上
        var instance = await workflow.AsTenantAsync(null, async sp =>
        {
            (await sp.GetRequiredService<IRepository<ApprovalGrant, string>>().AddAsync(new ApprovalGrant { Id = "seed", InstanceId = "none" }, CancellationToken.None)).ThrowIfFailure();
            await using var held = (await sp.GetRequiredService<IWorkflowInstanceLock>().AcquireAsync("instance-1", CancellationToken.None)).ValueOrThrow();
            Assert.Equal((1L, 0L), (Count(database.LeasePath, "WorkflowLockRecord"), Count(database.Path, "WorkflowLockRecord")));
            return (await sp.GetRequiredService<IWorkflowEngine>().StartAsync(new WorkflowStartRequest { Name = "grant" }, CancellationToken.None)).ValueOrThrow();
        });
        var task = Assert.Single(await workflow.PendingAsync("alice"));

        // 办理到结论、处理器写了授予，业务随后失败：授予与实例完成一起回滚
        await workflow.AsTenantAsync(null, async sp =>
        {
            (await sp.GetRequiredService<IWorkflowTaskService>().CompleteAsync(new UserTaskCompletion(task.Id, "alice", UserTaskOutcomes.Approved, null, null), CancellationToken.None)).ValueOrThrow();
            return true;
        }, rollback: true);
        Assert.Equal(WorkflowInstanceStatus.Running, (await workflow.InstanceAsync(instance.Id)).Status);

        // 提交：授予与实例完成一起落库；租约行都已释放
        await workflow.AsTenantAsync(null, async sp => (await sp.GetRequiredService<IWorkflowTaskService>().CompleteAsync(new UserTaskCompletion(task.Id, "alice", UserTaskOutcomes.Approved, null, null), CancellationToken.None)).ValueOrThrow());
        Assert.Equal(WorkflowInstanceStatus.Completed, (await workflow.InstanceAsync(instance.Id)).Status);
        Assert.Equal(2L, Count(database.Path, "ApprovalGrant"));
        Assert.Equal(0L, Count(database.LeasePath, "WorkflowLockRecord"));
    }

    /// <summary>
    /// 直接连库数一张表的行数。
    /// </summary>
    private static long Count(string path, string table)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";
        return (long)command.ExecuteScalar()!;
    }
}
