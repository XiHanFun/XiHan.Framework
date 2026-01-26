#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarConnectionConfigOptions
// Guid:3de67a9c-9f8a-4c79-8e2c-7a6c3d09e124
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2023-11-15 8:40:52
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;

namespace XiHan.Framework.Data.SqlSugar.Options;

/// <summary>
/// SqlSugar连接配置
/// </summary>
public class SqlSugarConnectionConfigOptions
{
    /// <summary>
    /// 配置唯一标识
    /// </summary>
    public string ConfigId { get; set; } = "Default";

    /// <summary>
    /// 连接字符串
    /// </summary>
    public string ConnectionString { get; set; } = null!;

    /// <summary>
    /// 数据库类型
    /// </summary>
    public DbType DbType { get; set; }

    /// <summary>
    /// 是否自动关闭连接
    /// </summary>
    public bool IsAutoCloseConnection { get; set; } = true;

    /// <summary>
    /// 初始化键类型
    /// </summary>
    public InitKeyType InitKeyType { get; set; } = InitKeyType.Attribute;

    /// <summary>
    /// 更多设置
    /// </summary>
    public ConnMoreSettings? MoreSettings { get; set; }

    /// <summary>
    /// 从库连接配置
    /// </summary>
    public List<SlaveConnectionConfig>? SlaveConnectionConfigs { get; set; }
}
