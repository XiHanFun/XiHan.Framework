#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SugarEntity
// Guid:d9af90d5-fc39-4ef1-bc1c-d6e35acef2a1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2023-11-15 9:05:42
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;
using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Domain.Entities;

namespace XiHan.Framework.Data.SqlSugar.Entities;

/// <summary>
/// SqlSugar 实体基类（泛型主键）
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class SugarEntity<TKey> : EntityBase<TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected SugarEntity()
    {
        RowVersion = new Version(1, 0, 0);
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">实体主键</param>
    protected SugarEntity(TKey basicId)
    {
        RowVersion = new Version(1, 0, 0);
        BasicId = basicId;
    }

    /// <summary>
    /// 版本控制标识，用于处理并发
    /// </summary>
    [ConcurrencyCheck]
    [Timestamp]
    [SugarColumn(IsOnlyIgnoreUpdate = true, ColumnDescription = "版本控制标识，用于处理并发")]
    public override Version RowVersion { get; set; }

    /// <summary>
    /// 主键
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = false, ColumnDescription = "主键标识")]
    public override TKey BasicId { get; protected set; } = default!;
}
