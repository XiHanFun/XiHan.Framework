// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 从 SQLite 的系统表读出一张表上的索引。
/// </summary>
internal static class SqliteSchema
{
    /// <summary>
    /// 表上显式建出的索引（不含主键的自动索引）：名字、是否唯一、按顺序的列。
    /// </summary>
    public static List<(string Name, bool IsUnique, string[] Columns)> Indexes(ISqlSugarClient client, string table)
    {
        // sql 为空的是 SQLite 为主键与 UNIQUE 约束自动建的索引，不是建表时显式建出的
        var names = client.Ado.SqlQuery<string>("SELECT name FROM sqlite_master WHERE type = 'index' AND tbl_name = @table AND sql IS NOT NULL ORDER BY name", new { table });
        return names
            .Select(name => (
                name,
                client.Ado.GetInt("SELECT \"unique\" FROM pragma_index_list(@table) WHERE name = @name", new { table, name }) == 1,
                client.Ado.SqlQuery<string>("SELECT name FROM pragma_index_info(@name) ORDER BY seqno", new { name }).ToArray()))
            .ToList();
    }
}
