// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace XiHan.Framework.Persistence.EntityFrameworkCore;

/// <summary>
/// SQLite 上 UTC ticks 时间列的启动核对。
/// </summary>
/// <remarks>
/// 约定把 <see cref="DateTimeOffset"/> 映射成 UTC ticks 整数（ADR-0091）之前建出的库，列里存的是 EF Core 的文本；按整数读时 SQLite 把文本开头的年份当整数，旧行被静默读成错误的时刻。这里逐列核对库里的存储形式：列的声明类型不是 INTEGER 亲和性即不符（空表也算），是 INTEGER 亲和性时抽样最早写入的 <see cref="SampleRows"/> 行、查到非整数的值即不符。只读，不改库（RFC-0005「EF Core 在 SQLite 上的时间列核对」，ADR-0094）。
/// </remarks>
internal static class SqliteTimestampCheck
{
    /// <summary>
    /// 每张表抽样的行数上限。
    /// </summary>
    /// <remarks>
    /// 旧行是最早写入的：重建表时原样搬进来的文本排在最前，抽样最早的行就能查到；上限让核对的开销与表的大小无关。
    /// </remarks>
    internal const int SampleRows = 1000;

    /// <summary>
    /// 找出存储形式与模型不符的时间列。
    /// </summary>
    /// <remarks>
    /// <paramref name="placedHere"/> 判定一个实体是否落在 <paramref name="context"/> 所在的连接上：只核对这些实体映射的表。Provider 不是 SQLite、没有要核对的列、库文件还不存在时返回空列表，不打开连接。返回 <c>表.列 (声明类型)</c>，抽样不符的另带查到的存储类。
    /// </remarks>
    public static async Task<IReadOnlyList<string>> FindMismatchesAsync(DbContext context, Func<Type, bool> placedHere, CancellationToken cancellationToken)
    {
        if (!string.Equals(context.Database.ProviderName, ModelBuilderExtensions.SqliteProvider, StringComparison.Ordinal))
        {
            return [];
        }

        var expected = TicksColumns(context.Model, placedHere);
        if (expected.Count == 0)
        {
            return [];
        }

        // 库文件还不存在就没有旧数据；SQLite 打开不存在的文件会建出空库，核对不能有这个副作用，所以先问 EF Core 库在不在
        if (!await context.GetService<IRelationalDatabaseCreator>().ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            return [];
        }

        await context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var connection = context.Database.GetDbConnection();
            var declared = await DeclaredTypesAsync(connection, cancellationToken).ConfigureAwait(false);
            var mismatches = new List<string>();
            foreach (var (table, columns) in expected)
            {
                // 库里没有的表或列跳过：迁移没跑到不归本核对，读写时 EF Core 自己报错
                if (!declared.TryGetValue(table, out var types))
                {
                    continue;
                }

                var sampled = new List<string>();
                foreach (var column in columns)
                {
                    if (!types.TryGetValue(column, out var type))
                    {
                        continue;
                    }

                    // TEXT 亲和性的列会把之后写入的整数也存成文本：空表也要现在失败，不等写入之后的下一次启动才被抽样查出来
                    if (HasIntegerAffinity(type))
                    {
                        sampled.Add(column);
                    }
                    else
                    {
                        mismatches.Add($"{table}.{column} ({type})");
                    }
                }

                if (sampled.Count > 0)
                {
                    await SampleAsync(connection, table, types, sampled, mismatches, cancellationToken).ConfigureAwait(false);
                }
            }

            return mismatches;
        }
        finally
        {
            await context.Database.CloseConnectionAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 声明类型是否是 INTEGER 亲和性。
    /// </summary>
    /// <remarks>
    /// SQLite 的亲和性规则第一条：声明类型含 <c>INT</c>（不区分大小写）即 INTEGER 亲和性，先于其余规则。
    /// </remarks>
    internal static bool HasIntegerAffinity(string declaredType) => declaredType.Contains("INT", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 落在这个连接上、由约定映射成 UTC ticks 的列，按表分组。
    /// </summary>
    /// <remarks>
    /// 取关系模型：从属类型、复杂类型与表拆分的列都在表上。表上任一非从属实体落在这个连接上，这张表就归它。只认约定施加的 <see cref="UtcTicksConverter"/>，应用自己配的转换不核对。
    /// </remarks>
    private static Dictionary<string, List<string>> TicksColumns(IModel model, Func<Type, bool> placedHere)
    {
        var tables = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in model.GetRelationalModel().Tables)
        {
            if (!table.EntityTypeMappings.Any(mapping => mapping.TypeBase is IEntityType entity && !entity.IsOwned() && placedHere(entity.ClrType)))
            {
                continue;
            }

            var columns = table.Columns
                .Where(static column => column.PropertyMappings.Any(static mapping => mapping.Property.GetTypeMapping().Converter is UtcTicksConverter))
                .Select(static column => column.Name)
                .ToList();
            if (columns.Count > 0)
            {
                tables[table.Name] = columns;
            }
        }

        return tables;
    }

    /// <summary>
    /// 读库里每张表每列的声明类型。
    /// </summary>
    /// <remarks>
    /// 一次查询读完：<c>sqlite_master</c> 联 <c>pragma_table_info</c>，只读库结构，与表里的行数无关。SQLite 的标识符不区分大小写，表名与列名按不区分大小写比较。
    /// </remarks>
    private static async Task<Dictionary<string, Dictionary<string, string>>> DeclaredTypesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var tables = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT m.name, p.name, p.type FROM sqlite_master AS m JOIN pragma_table_info(m.name) AS p WHERE m.type = 'table'";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var table = reader.GetString(0);
            if (!tables.TryGetValue(table, out var columns))
            {
                tables[table] = columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            columns[reader.GetString(1)] = reader.GetString(2);
        }

        return tables;
    }

    /// <summary>
    /// 抽样一张表最早写入的行，记下存了非整数值的列。
    /// </summary>
    /// <remarks>
    /// 一次查询：子查询 <c>NOT INDEXED</c> 按 rowid 顺序取最早的 <see cref="SampleRows"/> 行——走列上的索引会按值排序，整数排在文本之前，旧行就抽不到了。每列取查到的非整数存储类（<c>text</c>、<c>real</c>、<c>blob</c>），没有就是 NULL。
    /// </remarks>
    private static async Task SampleAsync(DbConnection connection, string table, Dictionary<string, string> types, List<string> columns, List<string> mismatches, CancellationToken cancellationToken)
    {
        var probes = string.Join(", ", columns.Select(static column => $"max(CASE WHEN typeof({ModelBuilderExtensions.Quote(column)}) IN ('integer', 'null') THEN NULL ELSE typeof({ModelBuilderExtensions.Quote(column)}) END)"));
        var selected = string.Join(", ", columns.Select(ModelBuilderExtensions.Quote));
        await using var command = connection.CreateCommand();
        command.CommandText = string.Create(CultureInfo.InvariantCulture, $"SELECT {probes} FROM (SELECT {selected} FROM {ModelBuilderExtensions.Quote(table)} NOT INDEXED LIMIT {SampleRows})");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        for (var i = 0; i < columns.Count; i++)
        {
            if (!await reader.IsDBNullAsync(i, cancellationToken).ConfigureAwait(false))
            {
                mismatches.Add($"{table}.{columns[i]} ({types[columns[i]]}, {reader.GetString(i)})");
            }
        }
    }
}
