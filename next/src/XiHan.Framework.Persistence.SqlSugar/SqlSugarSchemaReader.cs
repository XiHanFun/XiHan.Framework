// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.DataSources;
using XiHan.Framework.Persistence.Abstractions.Schema;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// SqlSugar 的库结构读取。
/// </summary>
/// <remarks>
/// 读表 <c>DbMaintenance.GetTableInfoList</c>、读列 <c>GetColumnInfosByTableName</c>（同步 API，在调用线程上执行，读列之前检查取消）。缺省连接另开一个不在事务里的连接（<c>CopyNew</c>）；运行时数据源经注册表按当前定义登记后另建客户端读。分表按已登记实体的模板折叠。驱动与 SqlSugar 的异常翻译为 <see cref="PersistenceErrors.DataSourceUnavailable"/>，原因脱敏；其余异常原样抛出。
/// </remarks>
/// <param name="services">根容器：在独立作用域里取应用的客户端。</param>
/// <param name="dataSources">运行时数据源注册表。</param>
/// <param name="catalog">实体目录：分表模板。</param>
internal sealed class SqlSugarSchemaReader(IServiceProvider services, DataSourceRegistry dataSources, SqlSugarEntityCatalog catalog) : ISchemaReader
{
    /// <summary>
    /// 有精度与小数位的类型名（定点数族）。
    /// </summary>
    private static readonly HashSet<string> _precisionTypes = new(StringComparer.OrdinalIgnoreCase) { "decimal", "dec", "numeric", "number", "money", "smallmoney" };

    /// <summary>
    /// 有声明长度的类型名（字符与二进制）。
    /// </summary>
    /// <remarks>
    /// 只有它们的长度是长度：别的类型数据库报告的是显示宽度（MySQL <c>tinyint(1)</c>）、小数秒位数（<c>datetime(6)</c>）、存储字节数或二进制精度（SQL Server <c>int</c> 的 10、<c>float</c> 的 53），当成长度会误导代码生成。
    /// </remarks>
    private static readonly HashSet<string> _lengthTypes = new(StringComparer.OrdinalIgnoreCase) { "char", "nchar", "varchar", "nvarchar", "varchar2", "nvarchar2", "character", "character varying", "bpchar", "binary", "varbinary", "raw" };

    /// <inheritdoc />
    public async Task<Result<TablePage>> ListTablesAsync(DataSourceDefinition? dataSource, TableListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        // 先核对分页：越界是调用方的输入错误，不连库、不登记运行时数据源
        if (query.Validate() is { IsFailure: true } invalid)
        {
            return Result.Failure<TablePage>(invalid.Error!);
        }

        var templates = catalog.GetSnapshot(cancellationToken).SplitTemplates;
        return await ReadAsync(dataSource, (client, _) =>
        {
            var tables = Fold(client, templates);
            var keyword = query.Keyword?.Trim();
            var matched = string.IsNullOrEmpty(keyword)
                ? tables
                : tables.FindAll(t => t.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) || (t.Comment?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false));
            return new TablePage([.. matched.Skip(query.Skip).Take(query.Take)], matched.Count);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<Result<TableSchema>>>> GetTablesAsync(DataSourceDefinition? dataSource, IReadOnlyList<string> tables, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tables);
        foreach (var table in tables)
        {
            ArgumentNullException.ThrowIfNull(table, nameof(tables));
        }

        // 上限约束一次调用的往返次数：读列是每张表一次查询
        if (tables.Count > SchemaLimits.MaxTablesPerRead)
        {
            return Error.Validation(PersistenceErrors.TakeExceeded, PersistenceMessages.TooManyTables, MessageArguments.Of(("count", tables.Count), ("max", SchemaLimits.MaxTablesPerRead)));
        }

        // 空集合不连库（批量优先第 5 条）
        if (tables.Count == 0)
        {
            return Result.Success<IReadOnlyList<Result<TableSchema>>>([]);
        }

        var templates = catalog.GetSnapshot(cancellationToken).SplitTemplates;
        var name = dataSource?.Name ?? DataSourceDefinition.DefaultName;
        return await ReadAsync(dataSource, (client, token) =>
        {
            var folded = Fold(client, templates);
            // 同名只读一次：重复的名字各自返回同一结构
            var read = new Dictionary<string, TableSchema>(StringComparer.Ordinal);
            var results = new List<Result<TableSchema>>(tables.Count);
            foreach (var requested in tables)
            {
                var table = Resolve(folded, requested);
                if (table is null)
                {
                    results.Add(Error.NotFound(PersistenceErrors.TableNotFound, PersistenceMessages.TableNotFound, MessageArguments.Of(("dataSource", name), ("table", requested))));
                    continue;
                }

                if (!read.TryGetValue(table.Name, out var schema))
                {
                    token.ThrowIfCancellationRequested();
                    // 分表的列取最新的物理表：旧分表建于加列之前时会少列
                    var columns = ReadColumns(client, table.PhysicalTables[0]);
                    schema = new TableSchema(table, [.. columns.Select(static (c, i) => Map(c, i))]);
                    read[table.Name] = schema;
                }

                results.Add(schema);
            }

            return (IReadOnlyList<Result<TableSchema>>)results;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 取客户端并读取。
    /// </summary>
    /// <remarks>
    /// 缺省连接：在独立作用域里取应用的客户端再 <c>CopyNew</c>——应用注册的 <c>SqlSugarScope</c> 在本调用链上可能开着事务，PostgreSQL 系语句失败会中止那个事务，读结构不能在里面做。运行时数据源：先按当前定义登记（定义不合法、登记已满、不支持按注册表的错误返回），再另建客户端，不碰共享客户端。
    /// </remarks>
    private async Task<Result<T>> ReadAsync<T>(DataSourceDefinition? dataSource, Func<SqlSugarClient, CancellationToken, T> read, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (dataSource is not null)
        {
            var registered = await dataSources.GetOrAddAsync(dataSource, cancellationToken).ConfigureAwait(false);
            if (!registered.TryGetValue(out var source))
            {
                return registered.Error!;
            }

            var sugar = source as SqlSugarDataSource ?? throw new InvalidOperationException($"Data source '{dataSource.Name}' was not created by the SqlSugar provider.");
            using var client = sugar.CreateClient();
            return Run(client, read, dataSource.Name, dataSource.ConnectionString, cancellationToken);
        }

        await using var scope = services.CreateAsyncScope();
        using var copy = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>().CopyNew();
        return Run(copy, read, DataSourceDefinition.DefaultName, copy.CurrentConnectionConfig.ConnectionString ?? string.Empty, cancellationToken);
    }

    /// <summary>
    /// 执行读取，把驱动与 SqlSugar 的异常翻译为读取失败。
    /// </summary>
    /// <remarks>
    /// 只翻译连接与查询类的异常（驱动的 <see cref="DbException"/>、SqlSugar 包装的异常、I/O 与超时）；编程错误原样抛出，不变成「连不上」。原因脱敏后才进错误。
    /// </remarks>
    private static Result<T> Run<T>(SqlSugarClient client, Func<SqlSugarClient, CancellationToken, T> read, string name, string connectionString, CancellationToken cancellationToken)
    {
        try
        {
            return read(client, cancellationToken);
        }
        catch (Exception exception) when (exception is DbException or SqlSugarException or IOException or TimeoutException)
        {
            return Error.Unavailable(PersistenceErrors.DataSourceUnavailable, PersistenceMessages.SchemaReadFailed, MessageArguments.Of(("name", name), ("reason", ConnectionStrings.Describe(exception, connectionString))));
        }
    }

    /// <summary>
    /// 读出全部表并把分表折叠成逻辑表，按名字排序。
    /// </summary>
    private static List<TableInfo> Fold(SqlSugarClient client, IReadOnlyList<SplitTableTemplate> templates)
    {
        var tables = new List<TableInfo>();
        var splits = new Dictionary<SplitTableTemplate, List<DbTableInfo>>();
        foreach (var table in client.DbMaintenance.GetTableInfoList(false))
        {
            var template = templates.FirstOrDefault(t => t.Matches(table.Name));
            if (template is null)
            {
                tables.Add(new TableInfo { Name = table.Name, Comment = NullIfBlank(table.Description), PhysicalTables = [table.Name] });
                continue;
            }

            if (!splits.TryGetValue(template, out var physical))
            {
                splits[template] = physical = [];
            }

            physical.Add(table);
        }

        foreach (var (template, physical) in splits)
        {
            // 新到旧：日期部分是定宽数字，表名倒序即时间倒序；注释与列取最新的物理表
            physical.Sort(static (left, right) => StringComparer.OrdinalIgnoreCase.Compare(right.Name, left.Name));
            tables.Add(new TableInfo { Name = template.LogicalName, Comment = NullIfBlank(physical[0].Description), PhysicalTables = [.. physical.Select(static p => p.Name)], IsSplit = true });
        }

        tables.Sort(static (left, right) =>
        {
            var compared = StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
            return compared != 0 ? compared : StringComparer.Ordinal.Compare(left.Name, right.Name);
        });
        return tables;
    }

    /// <summary>
    /// 按名字找表：先精确匹配，再按不区分大小写的唯一匹配。
    /// </summary>
    /// <remarks>
    /// 只差大小写的两张表（PostgreSQL 加引号的名字）不区分大小写时有歧义，只认精确的名字。
    /// </remarks>
    private static TableInfo? Resolve(List<TableInfo> tables, string name)
    {
        if (tables.Find(t => string.Equals(t.Name, name, StringComparison.Ordinal)) is { } exact)
        {
            return exact;
        }

        var candidates = tables.Where(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)).Take(2).ToList();
        return candidates.Count == 1 ? candidates[0] : null;
    }

    /// <summary>
    /// 读一张物理表的列。
    /// </summary>
    /// <remarks>
    /// Oracle 查数据字典（<see cref="OracleColumns"/>），别的数据库用 SqlSugar 的读列。
    /// </remarks>
    private static List<DbColumnInfo> ReadColumns(SqlSugarClient client, string table)
        => client.CurrentConnectionConfig.DbType == DbType.Oracle
            ? OracleColumns.Read(client, table)
            : client.DbMaintenance.GetColumnInfosByTableName(table, false);

    /// <summary>
    /// 把 SqlSugar 的列信息换成中立的列。
    /// </summary>
    /// <remarks>
    /// 定点数族的长度是精度、小数位取 <c>DecimalDigits</c>；字符与二进制类型的长度是声明的长度（0 与负数——<c>nvarchar(max)</c>——为 null）；其余类型没有长度。SQLite 对 <c>INTEGER PRIMARY KEY</c> 报告可空，主键列一律报告为不可空。
    /// </remarks>
    private static ColumnInfo Map(DbColumnInfo column, int ordinal)
    {
        var dataType = column.DataType ?? string.Empty;
        var baseType = dataType.Split('(')[0].Trim();
        var isPrecision = _precisionTypes.Contains(baseType);
        int? length = column.Length > 0 ? column.Length : null;
        return new ColumnInfo
        {
            Name = column.DbColumnName,
            DataType = dataType,
            Ordinal = ordinal,
            IsPrimaryKey = column.IsPrimarykey,
            IsIdentity = column.IsIdentity,
            IsNullable = column.IsNullable && !column.IsPrimarykey,
            Length = _lengthTypes.Contains(baseType) ? length : null,
            Precision = isPrecision ? length : null,
            Scale = isPrecision && length is not null ? Math.Max(column.DecimalDigits, 0) : null,
            Comment = NullIfBlank(column.ColumnDescription),
            DefaultValue = NullIfBlank(column.DefaultValue),
        };
    }

    /// <summary>
    /// 空白为 <see langword="null"/>。
    /// </summary>
    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
