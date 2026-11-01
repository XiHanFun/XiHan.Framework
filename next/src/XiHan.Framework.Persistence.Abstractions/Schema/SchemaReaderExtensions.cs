// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Persistence.Abstractions.DataSources;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Schema;

/// <summary>
/// <see cref="ISchemaReader"/> 的单表形式。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class SchemaReaderExtensions
{
    /// <summary>
    /// 读一张表的结构（含列）。
    /// </summary>
    /// <remarks>
    /// 是 <see cref="ISchemaReader.GetTablesAsync"/> 的单项特例：整批的失败与该项的失败（如 <see cref="PersistenceErrors.TableNotFound"/>）合成一个结果。读多张表时调批量方法，不要循环调用它。
    /// </remarks>
    public static async Task<Result<TableSchema>> GetTableAsync(this ISchemaReader reader, DataSourceDefinition? dataSource, string table, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(table);
        var batch = await reader.GetTablesAsync(dataSource, [table], cancellationToken).ConfigureAwait(false);
        return batch.TryGetValue(out var items) ? items[0] : batch.Error!;
    }
}
