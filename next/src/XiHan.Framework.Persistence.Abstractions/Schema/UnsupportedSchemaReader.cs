// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Persistence.Abstractions.DataSources;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Schema;

/// <summary>
/// 不能读库结构的 Provider 登记的读取器
/// </summary>
/// <remarks>
/// 两个方法都返回 <see cref="PersistenceErrors.NotSupported"/>，不返回空结果冒充
/// </remarks>
internal sealed class UnsupportedSchemaReader : ISchemaReader
{
    /// <inheritdoc />
    public Task<Result<TablePage>> ListTablesAsync(DataSourceDefinition? dataSource, TableListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return Task.FromResult<Result<TablePage>>(NotSupported());
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<Result<TableSchema>>>> GetTablesAsync(DataSourceDefinition? dataSource, IReadOnlyList<string> tables, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tables);
        return Task.FromResult<Result<IReadOnlyList<Result<TableSchema>>>>(NotSupported());
    }

    /// <summary>
    /// 不支持
    /// </summary>
    private static Error NotSupported() => Error.Unavailable(PersistenceErrors.NotSupported, PersistenceMessages.SchemaNotSupported);
}
