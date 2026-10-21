// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore;
using XiHan.Framework.Persistence.Conformance;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// 套件实体的 DbContext：显式注册三种实体并应用协议约定。
/// </summary>
public sealed class ConformanceDbContext(DbContextOptions<ConformanceDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConformanceOrder>();
        modelBuilder.Entity<ConformanceNote>();
        modelBuilder.Entity<ConformanceItem>(static item => item.Property(static i => i.Id).ValueGeneratedNever());
        modelBuilder.ApplyPersistenceConventions();
    }
}
