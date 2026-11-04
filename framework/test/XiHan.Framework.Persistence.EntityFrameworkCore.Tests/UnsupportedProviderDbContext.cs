// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// 按一个不支持部分唯一索引的 Provider 施加约定的 DbContext
/// </summary>
/// <remarks>
/// 连接用 SQLite，约定却按 MySQL 的 Provider 名施加：模型里有软删除实体的唯一索引，建模即失败。EF Core 按上下文类型缓存模型，所以单独一个类型，只给这一个用例用
/// </remarks>
public sealed class UnsupportedProviderDbContext(DbContextOptions<UnsupportedProviderDbContext> options) : DbContext(options)
{
    /// <summary>
    /// 登记软删除实体，按 MySQL 的 Provider 名施加约定
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SoftDeletedIndexedRow>();
        ModelBuilderExtensions.Apply(modelBuilder, "Pomelo.EntityFrameworkCore.MySql", relational: true);
    }
}
