// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Uow.Options;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.SqlSugar.Entities;
using XiHan.Framework.Workflow.SqlSugar.Stores;

namespace XiHan.Framework.Workflow.SqlSugar.Tests;

/// <summary>
/// 存储写入与外层事务隔离的真实数据库测试
/// </summary>
/// <remarks>
/// 地址取环境变量 <c>XIHAN_TEST_MYSQL</c>，未设置时跳过。
/// </remarks>
public class WorkflowIsolationMySqlTests
{
    private const string SkipReason = "未设置 XIHAN_TEST_MYSQL，跳过真实数据库测试。";

    private static readonly string? ConnectionString =
        Environment.GetEnvironmentVariable("XIHAN_TEST_MYSQL");

    /// <summary>
    /// 外层事务未提交时实例更新已对其他连接可见，外层回滚后更新仍在
    /// </summary>
    [Fact]
    public async Task 外层事务未提交时实例更新已对其他连接可见()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), SkipReason);

        using var database = WorkflowTestDatabase.CreateMySql(ConnectionString!);
        var store = new SqlSugarWorkflowInstanceStore(database.Executor);
        var instanceId = Guid.NewGuid().ToString("N");
        var outerDefinitionId = Guid.NewGuid().ToString("N");
        var instance = new WorkflowInstance
        {
            Id = instanceId,
            DefinitionId = "d1",
            DefinitionCode = "isolation",
            DefinitionVersion = 1,
            Name = "隔离",
            Status = WorkflowInstanceStatus.Running,
            CreationTime = new DateTime(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc)
        };
        await store.InsertAsync(instance);

        try
        {
            using (database.UnitOfWorkManager.Begin(new XiHanUnitOfWorkOptions(isTransactional: true)))
            {
                // 外层事务在同一个库上真正开启并持有一条未提交的写入
                await database.Resolver.GetClient(WorkflowTestDatabase.ConfigId)
                    .Insertable(new SysWorkflowDefinition(outerDefinitionId)
                    {
                        Code = "isolation-" + outerDefinitionId,
                        Name = "外层",
                        Version = 1
                    })
                    .ExecuteCommandAsync();

                instance.Status = WorkflowInstanceStatus.Suspended;
                await store.UpdateAsync(instance);

                using var probe = database.CreateProbeClient();
                var seen = await probe.Queryable<SysWorkflowInstance>()
                    .Where(item => item.BasicId == instanceId)
                    .ToListAsync();
                Assert.Equal((int)WorkflowInstanceStatus.Suspended, Assert.Single(seen).Status);

                // 外层不 Complete，随 Dispose 回滚
            }

            using var after = database.CreateProbeClient();
            var outerRows = await after.Queryable<SysWorkflowDefinition>()
                .Where(item => item.BasicId == outerDefinitionId)
                .ToListAsync();
            Assert.Empty(outerRows);

            var persisted = await after.Queryable<SysWorkflowInstance>()
                .Where(item => item.BasicId == instanceId)
                .ToListAsync();
            Assert.Equal((int)WorkflowInstanceStatus.Suspended, Assert.Single(persisted).Status);
        }
        finally
        {
            await store.DeleteAsync(instanceId);
        }
    }
}
