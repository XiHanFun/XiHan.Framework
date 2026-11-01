// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using XiHan.Framework.Persistence.EntityFrameworkCore;
using XiHan.Framework.Workflow.Database.Entities;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// EF Core 上跑数据库工作流的应用 DbContext：九张工作流表加用例的授予表。
/// </summary>
/// <remarks>
/// SQLite 不能比较与排序 <see cref="DateTimeOffset"/>（EF Core 存成文本），到期书签与实例列表都按时间过滤和排序，所以按应用的惯常做法换成整数存储；框架写入的都是 UTC 时刻，换算后保序。
/// </remarks>
public sealed class WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : DbContext(options)
{
    /// <summary>
    /// SQLite 上的时间列存成整数。
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
    }

    /// <summary>
    /// 登记工作流的九张表与授予表，再施加持久化约定（并发令牌、索引）。
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
