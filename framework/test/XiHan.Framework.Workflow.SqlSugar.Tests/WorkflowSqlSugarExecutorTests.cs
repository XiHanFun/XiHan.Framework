// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.Uow.Options;
using XiHan.Framework.Workflow.SqlSugar.Entities;
using XiHan.Framework.Workflow.SqlSugar.Options;
using XiHan.Framework.Workflow.SqlSugar.Stores;

namespace XiHan.Framework.Workflow.SqlSugar.Tests;

/// <summary>
/// 执行器测试
/// </summary>
public class WorkflowSqlSugarExecutorTests : IDisposable
{
    private readonly WorkflowTestDatabase _database = WorkflowTestDatabase.CreateSqlite();

    /// <summary>
    /// 写入不随外层工作单元回滚
    /// </summary>
    [Fact]
    public async Task 写入不随外层工作单元回滚()
    {
        using (_database.UnitOfWorkManager.Begin(new XiHanUnitOfWorkOptions(isTransactional: true)))
        {
            await _database.Executor.ExecuteAsync(
                client => client.Insertable(NewDefinition("isolated")).ExecuteCommandAsync(CancellationToken.None),
                CancellationToken.None);

            // 外层不 Complete，随 Dispose 回滚
        }

        using var probe = _database.CreateProbeClient();
        Assert.Equal(1, probe.Queryable<SysWorkflowDefinition>().Count());
    }

    /// <summary>
    /// 操作抛出异常时写入回滚
    /// </summary>
    [Fact]
    public async Task 操作抛出异常时写入回滚()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _database.Executor.ExecuteAsync<int>(
            async client =>
            {
                await client.Insertable(NewDefinition("rollback")).ExecuteCommandAsync(CancellationToken.None);
                throw new InvalidOperationException("模拟失败");
            },
            CancellationToken.None));

        using var probe = _database.CreateProbeClient();
        Assert.Equal(0, probe.Queryable<SysWorkflowDefinition>().Count());
    }

    /// <summary>
    /// 已取消的令牌不执行操作
    /// </summary>
    [Fact]
    public async Task 已取消的令牌不执行操作()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var invoked = false;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _database.Executor.ExecuteAsync(
            client =>
            {
                invoked = true;
                return Task.FromResult(0);
            },
            cancellation.Token));

        Assert.False(invoked);
    }

    /// <summary>
    /// 连接配置标识默认取数据访问的默认连接
    /// </summary>
    [Fact]
    public void 连接配置标识默认取数据访问的默认连接()
    {
        var executor = CreateExecutor(configId: "  ");

        Assert.Equal("Default", executor.ConfigId);
    }

    /// <summary>
    /// 连接配置标识可由选项覆盖
    /// </summary>
    [Fact]
    public void 连接配置标识可由选项覆盖()
    {
        var executor = CreateExecutor(configId: " Workflow ");

        Assert.Equal("Workflow", executor.ConfigId);
    }

    /// <summary>
    /// 释放测试夹具
    /// </summary>
    public void Dispose()
    {
        _database.Dispose();
    }

    private WorkflowSqlSugarExecutor CreateExecutor(string? configId)
    {
        return new WorkflowSqlSugarExecutor(
            _database.Resolver,
            _database.UnitOfWorkManager,
            Microsoft.Extensions.Options.Options.Create(new XiHanSqlSugarCoreOptions()),
            Microsoft.Extensions.Options.Options.Create(new XiHanWorkflowSqlSugarOptions { ConfigId = configId }));
    }

    private static SysWorkflowDefinition NewDefinition(string code)
    {
        return new SysWorkflowDefinition(Guid.NewGuid().ToString("N"))
        {
            Code = code,
            Name = code,
            Version = 1,
            CreationTime = new DateTime(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc)
        };
    }
}
