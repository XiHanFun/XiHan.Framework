// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore;
using XiHan.Framework.Messaging.Abstractions.Outbox;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// 发件箱一致性套件的 DbContext：只登记发件箱实体并应用协议约定
/// </summary>
/// <remarks>
/// 与应用的做法一致：<see cref="OutboxMessage"/> 进模型，再 <c>ApplyPersistenceConventions</c> 得到实体上声明的索引与 SQLite 的时间列存储
/// </remarks>
public sealed class OutboxDbContext(DbContextOptions<OutboxDbContext> options) : DbContext(options)
{
    /// <summary>
    /// 登记发件箱实体并应用协议约定
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>();
        modelBuilder.ApplyPersistenceConventions(Database);
    }
}
