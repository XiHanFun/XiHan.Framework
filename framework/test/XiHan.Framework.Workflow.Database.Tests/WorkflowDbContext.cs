// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore;
using XiHan.Framework.Persistence.EntityFrameworkCore;
using XiHan.Framework.Workflow.Database.Entities;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// EF Core 上跑数据库工作流的应用 DbContext：九张工作流表加用例的授予表
/// </summary>
/// <remarks>
/// 不自己配 <see cref="DateTimeOffset"/> 的存储：SQLite 上 <c>ApplyPersistenceConventions(Database)</c> 把它存成 UTC ticks 整数，到期书签与实例列表按时间过滤和排序照常可用（ADR-0091）
/// </remarks>
public sealed class WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : DbContext(options)
{
    /// <summary>
    /// 登记工作流的九张表与授予表，再施加持久化约定（并发令牌、索引）
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.Entity<WorkflowDefinitionRecord>();
        modelBuilder.Entity<WorkflowInstanceRecord>();
        modelBuilder.Entity<WorkflowNodeInstanceRecord>();
        modelBuilder.Entity<WorkflowBookmarkRecord>();
        modelBuilder.Entity<PlatformWorkflowDefinitionRecord>();
        modelBuilder.Entity<PlatformWorkflowInstanceRecord>();
        modelBuilder.Entity<PlatformWorkflowNodeInstanceRecord>();
        modelBuilder.Entity<PlatformWorkflowBookmarkRecord>();
        modelBuilder.Entity<WorkflowLockRecord>();
        modelBuilder.Entity<ApprovalGrant>();
        modelBuilder.ApplyPersistenceConventions(Database);
    }
}
