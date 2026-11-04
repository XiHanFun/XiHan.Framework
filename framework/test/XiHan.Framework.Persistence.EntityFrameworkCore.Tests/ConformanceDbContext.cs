// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore;
using XiHan.Framework.Persistence.Conformance.Fixtures;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// 套件实体的 DbContext：显式注册四种实体并应用协议约定
/// </summary>
public sealed class ConformanceDbContext(DbContextOptions<ConformanceDbContext> options) : DbContext(options)
{
    /// <summary>
    /// 注册四种套件实体，再统一应用并发令牌与索引约定
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConformanceOrder>();
        modelBuilder.Entity<ConformanceNote>();
        // 整数标识由用例自行指定，不能交给数据库自增，否则插入 Id = 7 会被覆盖
        modelBuilder.Entity<ConformanceItem>(static item => item.Property(static i => i.Id).ValueGeneratedNever());
        // 一致性套件把它登记到命名连接（用例 32）；模型里有它，命名连接的库才建出它的表
        modelBuilder.Entity<ConformanceLease>();
        modelBuilder.ApplyPersistenceConventions(Database);
    }
}
