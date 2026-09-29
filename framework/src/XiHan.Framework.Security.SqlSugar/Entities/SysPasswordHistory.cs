// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Entities;

namespace XiHan.Framework.Security.SqlSugar.Entities;

/// <summary>
/// 密码历史记录实体
/// </summary>
[SugarTable("sys_password_history")]
[SugarIndex("idx_sys_password_history_user",
    nameof(SysPasswordHistory.UserId), OrderByType.Asc,
    nameof(SysPasswordHistory.CreatedTime), OrderByType.Desc)]
public class SysPasswordHistory : SugarCreationEntity<long>
{
    /// <summary>
    /// 构造函数，供 SqlSugar 物化实体使用
    /// </summary>
    public SysPasswordHistory() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    public SysPasswordHistory(long basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 用户标识
    /// </summary>
    [SugarColumn(ColumnName = "User_Id", IsNullable = false, ColumnDescription = "用户标识")]
    public long UserId { get; set; }

    /// <summary>
    /// 密码哈希
    /// </summary>
    [SugarColumn(ColumnName = "Password_Hash", Length = 512, IsNullable = false, ColumnDescription = "密码哈希")]
    public string PasswordHash { get; set; } = string.Empty;
}
