#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarDatabaseMetadataProvider
// Guid:bd014d54-d1bf-4667-81f7-aa95324abe47
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/28 03:11:43
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using SqlSugar;

namespace XiHan.Framework.Data.SqlSugar.Metadata;

/// <summary>
/// 基于 SqlSugar 的数据库元数据提供器
/// </summary>
public sealed class SqlSugarDatabaseMetadataProvider : IDatabaseMetadataProvider
{
    private readonly ISqlSugarClientProvider _clientProvider;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clientProvider">数据库客户端提供器</param>
    public SqlSugarDatabaseMetadataProvider(ISqlSugarClientProvider clientProvider)
    {
        _clientProvider = clientProvider;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DatabaseTableMetadata>> GetTablesAsync(string? connectionConfigId = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await Task.Run(() =>
        {
            var dbClient = ResolveClient(connectionConfigId);
            var tables = dbClient.DbMaintenance.GetTableInfoList(false);
            return (IReadOnlyList<DatabaseTableMetadata>)tables
                .Select(MapTableInfo)
                .ToList();
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DatabaseTableMetadata?> GetTableAsync(string tableName, string? connectionConfigId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        var tables = await GetTablesAsync(connectionConfigId, cancellationToken);
        var table = tables.FirstOrDefault(t => string.Equals(t.TableName, tableName, StringComparison.OrdinalIgnoreCase));
        if (table == null)
        {
            return null;
        }

        var columns = await GetColumnsAsync(table.TableName, connectionConfigId, cancellationToken);
        table.Columns = columns;
        return table;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DatabaseColumnMetadata>> GetColumnsAsync(string tableName, string? connectionConfigId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        cancellationToken.ThrowIfCancellationRequested();

        return await Task.Run(() =>
        {
            var dbClient = ResolveClient(connectionConfigId);
            var columns = dbClient.DbMaintenance.GetColumnInfosByTableName(tableName, false);
            return (IReadOnlyList<DatabaseColumnMetadata>)columns
                .Select(MapColumnInfo)
                .ToList();
        }, cancellationToken);
    }

    private ISqlSugarClient ResolveClient(string? connectionConfigId)
    {
        if (string.IsNullOrWhiteSpace(connectionConfigId))
        {
            return _clientProvider.GetClient();
        }

        return _clientProvider.GetScope().GetConnectionScope(connectionConfigId);
    }

    private static DatabaseTableMetadata MapTableInfo(object tableInfo)
    {
        return new DatabaseTableMetadata
        {
            TableName = ReadString(tableInfo, "Name", "TableName") ?? string.Empty,
            TableDescription = ReadString(tableInfo, "Description", "TableDescription", "Comment")
        };
    }

    private static DatabaseColumnMetadata MapColumnInfo(object columnInfo)
    {
        return new DatabaseColumnMetadata
        {
            ColumnName = ReadString(columnInfo, "DbColumnName", "ColumnName", "Name") ?? string.Empty,
            DataType = ReadString(columnInfo, "DataType", "DataTypeName", "SqlParameterDbType", "ColumnDataType"),
            IsNullable = ReadBool(columnInfo, "IsNullable"),
            IsPrimaryKey = ReadBool(columnInfo, "IsPrimarykey", "IsPrimaryKey"),
            IsIdentity = ReadBool(columnInfo, "IsIdentity"),
            Length = ReadInt(columnInfo, "Length", "DataLength"),
            Scale = ReadInt(columnInfo, "DecimalDigits", "Scale"),
            DefaultValue = ReadString(columnInfo, "DefaultValue"),
            Description = ReadString(columnInfo, "ColumnDescription", "Description", "Comment")
        };
    }

    private static string? ReadString(object source, params string[] propertyNames)
    {
        var value = ReadObject(source, propertyNames);
        return value?.ToString();
    }

    private static bool ReadBool(object source, params string[] propertyNames)
    {
        var value = ReadObject(source, propertyNames);
        return value switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var result) => result,
            _ => false
        };
    }

    private static int? ReadInt(object source, params string[] propertyNames)
    {
        var value = ReadObject(source, propertyNames);
        if (value == null)
        {
            return null;
        }

        if (value is int intValue)
        {
            return intValue;
        }

        if (int.TryParse(value.ToString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static object? ReadObject(object source, params string[] propertyNames)
    {
        var sourceType = source.GetType();
        foreach (var propertyName in propertyNames)
        {
            var property = sourceType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property == null)
            {
                continue;
            }

            var value = property.GetValue(source);
            if (value != null)
            {
                return value;
            }
        }

        return null;
    }
}
