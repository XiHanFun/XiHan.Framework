// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// 时刻列测试的 DbContext：只登记 <see cref="TimedRow"/>，自己不配任何值转换。
/// </summary>
public sealed class TimedDbContext(DbContextOptions<TimedDbContext> options) : DbContext(options)
{
    /// <summary>
    /// 登记实体并应用协议约定。
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TimedRow>();
        modelBuilder.ApplyPersistenceConventions(Database);
    }
}
