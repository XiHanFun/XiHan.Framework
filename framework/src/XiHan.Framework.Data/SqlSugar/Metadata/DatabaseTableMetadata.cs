// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
