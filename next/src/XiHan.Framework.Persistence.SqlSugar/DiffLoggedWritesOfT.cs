// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// 带差异日志的写入：SqlSugar 的按对象入口（<c>InsertableByObject</c> 等）不能同时带差异日志与分表，这里走泛型构建器并调 <c>EnableDiffLogEvent</c>。
/// 插入与更新的分表入口沿用差异日志开关；分表删除（<c>Deleteable().SplitTable()</c>）按表重建删除构建器、丢掉开关，所以由这里按 <c>[SplitField]</c> 的值分组到表，每张表一次按主键删除。
/// 实体须有公开无参构造函数，与 SqlSugar 按对象入口的要求相同。
/// </summary>
/// <typeparam name="T">实体类型。</typeparam>
internal sealed class DiffLoggedWrites<T> : IDiffLoggedWrites
    where T : class, new()
{
    /// <inheritdoc />
    public Task<int> InsertAsync(ISqlSugarClient client, object entityOrList, bool split, SqlSugarDiffLogData data)
    {
        var insertable = (entityOrList is List<T> list ? client.Insertable(list) : client.Insertable((T)entityOrList)).EnableDiffLogEvent(data);
        return split ? insertable.SplitTable().ExecuteCommandAsync() : insertable.ExecuteCommandAsync();
    }

    /// <inheritdoc />
    public Task<int> UpdateAsync(ISqlSugarClient client, object entityOrList, bool split, SqlSugarDiffLogData data)
    {
        var updateable = (entityOrList is List<T> list ? client.Updateable(list) : client.Updateable((T)entityOrList)).EnableDiffLogEvent(data);
        return split ? updateable.SplitTable().ExecuteCommandAsync() : updateable.ExecuteCommandAsync();
    }

    /// <inheritdoc />
    public async Task<int> DeleteAsync(ISqlSugarClient client, object entityOrList, bool split, SqlSugarDiffLogData data)
    {
        var rows = entityOrList as List<T> ?? [(T)entityOrList];
        if (!split)
        {
            return await client.Deleteable(rows).EnableDiffLogEvent(data).ExecuteCommandAsync().ConfigureAwait(false);
        }

        // 每张分表一条按主键的删除语句，差异日志的写前值取自同一张表
        var deleted = 0;
        foreach (var table in rows.GroupBy(row => client.SplitHelper(row).GetTableName()))
        {
            deleted += await client.Deleteable(table.ToList()).AS(table.Key).EnableDiffLogEvent(data).ExecuteCommandAsync().ConfigureAwait(false);
        }

        return deleted;
    }
}
