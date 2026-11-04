// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.DataSources;

/// <summary>
/// 常用的数据库类型名
/// </summary>
/// <remarks>
/// <see cref="DataSourceDefinition.DatabaseType"/> 不区分大小写；Provider 认得的名字可以更多（SqlSugar 接受它的 <c>DbType</c> 枚举名），不认得的以 <see cref="PersistenceErrors.DataSourceInvalid"/> 拒绝
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class DatabaseTypes
{
    /// <summary>
    /// PostgreSQL
    /// </summary>
    public const string PostgreSql = "postgresql";

    /// <summary>
    /// MySQL
    /// </summary>
    public const string MySql = "mysql";

    /// <summary>
    /// SQL Server
    /// </summary>
    public const string SqlServer = "sqlserver";

    /// <summary>
    /// SQLite
    /// </summary>
    public const string Sqlite = "sqlite";

    /// <summary>
    /// Oracle
    /// </summary>
    public const string Oracle = "oracle";
}
