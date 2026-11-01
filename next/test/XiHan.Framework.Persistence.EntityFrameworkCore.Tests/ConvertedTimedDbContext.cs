// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// 应用自己配了时刻列存储的 DbContext：约定不能改掉它们。
/// </summary>
/// <remarks>
/// <see cref="TimedRow.At"/> 经 <c>ConfigureConventions</c> 统一换成 <see cref="DateTimeOffsetToBinaryConverter"/>（第五批测试里应用的写法），<see cref="TimedRow.Due"/> 在 <c>OnModelCreating</c> 里显式存成文本。
/// </remarks>
public sealed class ConvertedTimedDbContext(DbContextOptions<ConvertedTimedDbContext> options) : DbContext(options)
{
    /// <summary>
    /// 时刻列统一存成二进制整数。
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
    }

    /// <summary>
    /// 登记实体，把到期时刻显式存成文本，再应用协议约定。
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TimedRow>().Property(static row => row.Due).HasConversion<string>();
        modelBuilder.ApplyPersistenceConventions(Database);
    }
}
