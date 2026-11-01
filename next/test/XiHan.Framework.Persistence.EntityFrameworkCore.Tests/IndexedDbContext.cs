// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// 索引测试的 DbContext：登记两种声明了索引的实体，先配表名再应用协议约定。
/// </summary>
public sealed class IndexedDbContext(DbContextOptions<IndexedDbContext> options) : DbContext(options)
{
    /// <summary>
    /// 登记两种实体并应用协议约定。
    /// </summary>
    /// <remarks>
    /// <see cref="IndexedRow"/> 的表名改成 <c>indexed_row</c>，在应用约定之前配置。
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IndexedRow>().ToTable("indexed_row");
        modelBuilder.Entity<SoftDeletedIndexedRow>();
        modelBuilder.ApplyPersistenceConventions();
    }
}
