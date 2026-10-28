// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// 开启了差异日志的实体的写入入口；仓储的实体类型没有 <c>new()</c> 约束，按闭合类型构造 <see cref="DiffLoggedWrites{T}"/> 后经本接口调用。
/// </summary>
internal interface IDiffLoggedWrites
{
    /// <summary>
    /// 插入单个实体或实体列表，带差异日志；<paramref name="split"/> 时按分表字段落表。
    /// </summary>
    Task<int> InsertAsync(ISqlSugarClient client, object entityOrList, bool split, SqlSugarDiffLogData data);

    /// <summary>
    /// 按主键整行更新单个实体或实体列表，带差异日志；<paramref name="split"/> 时按分表字段定位所在的表。
    /// </summary>
    Task<int> UpdateAsync(ISqlSugarClient client, object entityOrList, bool split, SqlSugarDiffLogData data);

    /// <summary>
    /// 按主键物理删除单个实体或实体列表，带差异日志；<paramref name="split"/> 时按分表字段定位所在的表。
    /// </summary>
    Task<int> DeleteAsync(ISqlSugarClient client, object entityOrList, bool split, SqlSugarDiffLogData data);
}
