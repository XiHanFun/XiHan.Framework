#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SugarCreationEntity
// Guid:2446e621-2282-4241-9b62-eea59a563f3e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/25 5:25:16
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;
using XiHan.Framework.Domain.Entities;

namespace XiHan.Framework.Data.SqlSugar.Entities;

/// <summary>
/// SqlSugar 创建审计实体基类
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class SugarCreationEntity<TKey> : CreationEntityBase<TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected SugarCreationEntity()
    {
        CreatedTime = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "创建时间")]
    public override DateTimeOffset CreatedTime { get; set; }

    /// <summary>
    /// 创建者唯一标识
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "创建者唯一标识")]
    public override TKey? CreatedId { get; set; }

    /// <summary>
    /// 创建人
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "创建人")]
    public override string? CreatedBy { get; set; }
}
