// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
