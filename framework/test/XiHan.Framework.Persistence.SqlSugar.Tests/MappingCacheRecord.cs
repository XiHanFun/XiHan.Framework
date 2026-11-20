// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Domain.Entities;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 列定义随数据库类型而变的实体
/// </summary>
/// <remarks>
/// 只给映射缓存的用例用：别的用例不取它的映射，缓存里没有先建好的那份
/// </remarks>
internal sealed class MappingCacheRecord : IEntity<Guid>
{
    /// <inheritdoc />
    public Guid Id { get; init; }

    /// <summary>
    /// 有长度的字符串：SQL Server 上是 nvarchar
    /// </summary>
    [StringLength(32)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 时刻：SQL Server 上写入参数按 datetime2 传
    /// </summary>
    public DateTimeOffset At { get; set; }
}
