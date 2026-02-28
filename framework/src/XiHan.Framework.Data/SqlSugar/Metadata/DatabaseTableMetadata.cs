#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DatabaseTableMetadata
// Guid:4443fe63-57f0-4439-a0e1-9a875847e47c
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
