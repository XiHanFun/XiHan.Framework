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
    /// 初始化当前租户所在的这一整套布局：主库加上该租户自带的模块库
    /// </summary>
    /// <remarks>
    /// 给「只初始化这一个租户」的场景用（如库隔离租户开通时建库建表）。
    /// 与 <see cref="InitializeAsync"/> 的区别是不遍历静态配置里的全部库，
    /// 也不改动当前租户上下文——调用方切到目标租户后调它即可。
    /// </remarks>
    Task InitializeCurrentLayoutAsync();

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
