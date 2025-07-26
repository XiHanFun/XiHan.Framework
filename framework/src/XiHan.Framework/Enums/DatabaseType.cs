#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DatabaseType
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5d2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Enums;

/// <summary>
/// 数据库类型
/// </summary>
public enum DatabaseType
{
    /// <summary>
    /// SQL Server
    /// </summary>
    SqlServer = 0,

    /// <summary>
    /// MySQL
    /// </summary>
    MySql = 1,

    /// <summary>
    /// PostgreSQL
    /// </summary>
    PostgreSql = 2,

    /// <summary>
    /// SQLite
    /// </summary>
    Sqlite = 3,

    /// <summary>
    /// Oracle
    /// </summary>
    Oracle = 4,

    /// <summary>
    /// MongoDB
    /// </summary>
    MongoDb = 5,

    /// <summary>
    /// Redis
    /// </summary>
    Redis = 6,

    /// <summary>
    /// 内存数据库
    /// </summary>
    InMemory = 7
}
