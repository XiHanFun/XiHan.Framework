// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
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
    protected SugarEntity() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">实体主键</param>
    protected SugarEntity(TKey basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 版本控制标识，用于处理并发
    /// </summary>
    [SugarColumn(ColumnName = "Row_Version", ColumnDescription = "版本控制标识，用于处理并发", IsEnableUpdateVersionValidation = true)]
    public override long RowVersion { get; set; }

    /// <summary>
    /// 主键
    /// </summary>
    [SugarColumn(ColumnName = "Basic_Id", IsPrimaryKey = true, IsIdentity = false, ColumnDescription = "主键标识")]
    public override TKey BasicId { get; protected set; } = default!;
}
