// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Data.SqlSugar.Initializers;

/// <summary>
/// 存量库表结构升级器
/// </summary>
/// <remarks>
/// <para>
/// 数据库初始化分三段：全部连接建库建表 → 表结构升级 → 播种。建表只创建缺失的表、不改已存在的表，
/// 存量库的新列、新索引由升级器（通常是按版本执行的升级脚本）补齐；种子按最新实体读写，
/// 必须排在升级之后，否则种子一查就撞上还没补齐的列，启动失败。
/// </para>
/// <para>
/// 升级器只在开启表结构初始化时由 <see cref="IDbInitializer.InitializeAsync"/> 调用，失败直接抛出、中断初始化；
/// 新库上表是按最新实体建的，升级器应当空转。
/// </para>
/// </remarks>
public interface IDbSchemaUpgrader
{
    /// <summary>
    /// 把存量库的表结构升级到当前版本
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpgradeAsync(CancellationToken cancellationToken = default);
}
