#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DatabaseMetadataModels
// Guid:042d794e-656b-4f6a-9928-81bf5a1bf2e0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/28 03:11:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Data.SqlSugar.Metadata;

/// <summary>
/// 数据库表元数据
/// </summary>
public sealed class DatabaseTableMetadata
{
    /// <summary>
    /// 表名
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// 表描述
    /// </summary>
    public string? TableDescription { get; set; }

    /// <summary>
    /// 字段列表
    /// </summary>
    public IReadOnlyList<DatabaseColumnMetadata> Columns { get; set; } = [];
}

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
