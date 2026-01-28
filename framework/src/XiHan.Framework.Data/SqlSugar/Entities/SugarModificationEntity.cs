#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SugarModificationEntity
// Guid:dd381206-1bb9-4901-a436-290500d490a7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/25 5:26:31
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;
using XiHan.Framework.Domain.Entities;

namespace XiHan.Framework.Data.SqlSugar.Entities;

/// <summary>
/// SqlSugar 修改审计实体基类
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class SugarModificationEntity<TKey> : ModificationEntityBase<TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected SugarModificationEntity() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    protected SugarModificationEntity(TKey basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    /// <param name="modifiedId">修改者ID</param>
    protected SugarModificationEntity(TKey basicId, TKey modifiedId) : base(basicId, modifiedId)
    {
    }

    /// <summary>
    /// 版本控制标识，用于处理并发
    /// </summary>
    [SugarColumn(ColumnDescription = "版本控制标识，用于处理并发")]
    public override long RowVersion { get; set; }

    /// <summary>
    /// 主键
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, ColumnDescription = "主键")]
    public override TKey BasicId { get; protected set; } = default!;

    /// <summary>
    /// 修改时间
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "修改时间")]
    public override DateTimeOffset? ModifiedTime { get; set; }

    /// <summary>
    /// 修改者唯一标识
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "修改者唯一标识")]
    public override TKey? ModifiedId { get; set; }

    /// <summary>
    /// 修改人
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "修改人")]
    public override string? ModifiedBy { get; set; }
}
