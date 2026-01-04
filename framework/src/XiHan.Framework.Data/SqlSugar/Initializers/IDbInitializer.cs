#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IDbInitializer
// Guid:5b6c7d8e-9f0a-1b2c-3d4e-5f6a7b8c9d0e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025-01-05 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Data.SqlSugar.Initializers;

/// <summary>
/// 数据库初始化器接口
/// </summary>
public interface IDbInitializer
{
    /// <summary>
    /// 初始化数据库
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// 创建数据库（如果不存在）
    /// </summary>
    Task CreateDatabaseAsync();

    /// <summary>
    /// 创建表结构
    /// </summary>
    Task CreateTablesAsync();

    /// <summary>
    /// 执行种子数据
    /// </summary>
    Task SeedDataAsync();
}
