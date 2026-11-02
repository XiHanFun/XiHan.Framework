// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// 旧库的 DbContext：时刻列按 EF Core 缺省存成文本。
/// </summary>
/// <remarks>
/// 不调用 <c>ApplyPersistenceConventions</c>，相当于约定把时刻映射成 UTC ticks（ADR-0091）之前的应用建出的库：<see cref="TimedRow.At"/> 与 <see cref="TimedRow.Due"/> 都是 <c>TEXT</c> 列，存 <c>2026-11-01 08:00:00+08:00</c> 这样的文本。
/// </remarks>
public sealed class LegacyTimedDbContext(DbContextOptions<LegacyTimedDbContext> options) : DbContext(options)
{
    /// <summary>
    /// 只登记实体，不施加协议约定。
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<TimedRow>();
}
