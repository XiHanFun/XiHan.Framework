// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;
using XiHan.Framework.Persistence.Abstractions.DataSources;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// 从中立的数据源取 SqlSugar 客户端。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class SqlSugarDataSourceExtensions
{
    /// <summary>
    /// 数据源的 SqlSugar 客户端（<see cref="SqlSugarDataSource.Client"/>）。
    /// </summary>
    /// <exception cref="ArgumentException">数据源不是 SqlSugar 建的。</exception>
    public static ISqlSugarClient GetSqlSugarClient(this IDataSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source is SqlSugarDataSource sugar
            ? sugar.Client
            : throw new ArgumentException($"Data source '{source.Name}' was not created by the SqlSugar provider.", nameof(source));
    }
}
