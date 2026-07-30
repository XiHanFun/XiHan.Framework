// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Data.SqlSugar.Metadata;

/// <summary>
/// 数据库列元数据
/// </summary>
public sealed class DatabaseColumnMetadata
{
    /// <summary>
    /// 列名
    /// </summary>
    public string ColumnName { get; set; } = string.Empty;

    /// <summary>
    /// 数据库类型
    /// </summary>
    public string? DataType { get; set; }

    /// <summary>
    /// 是否可空
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    /// 是否主键
    /// </summary>
    public bool IsPrimaryKey { get; set; }

    /// <summary>
    /// 是否自增
    /// </summary>
    public bool IsIdentity { get; set; }

    /// <summary>
    /// 长度
    /// </summary>
    public int? Length { get; set; }

    /// <summary>
    /// 小数位
    /// </summary>
    public int? Scale { get; set; }

    /// <summary>
    /// 默认值
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// 字段注释
    /// </summary>
    public string? Description { get; set; }
}
