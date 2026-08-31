// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using System.Data.Common;
using XiHan.Framework.Data.SqlSugar.Routing;

namespace XiHan.Framework.Data.SqlSugar.Tenanting;

/// <summary>
/// 从租户主库连接串派生该租户模块库连接串的约定
/// </summary>
/// <remarks>
/// <para>
/// 规则：把连接串里的库名换成 <c>{租户库名}_{模块名}</c>（SQLite 换的是库文件名），其余字段原样保留。
/// 分隔符与 ConfigId 派生规则共用 <see cref="ModuleDataSourceConfigIds.Separator"/>，
/// 于是 <c>Tenant_1001</c> 这套布局里，连接标识 <c>Tenant_1001_Erp</c> 与库名 <c>qqq_Erp</c> 是同一套读法。
/// </para>
/// <para>
/// 库名字段按数据库类型识别；识别不出来直接抛，不静默回落共享模块库——
/// 那会让声明了库隔离的租户在毫无提示的情况下把模块数据写进公共库。
/// </para>
/// </remarks>
public static class TenantModuleConnectionStringDeriver
{
    /// <summary>
    /// 关系库的库名字段候选（大小写不敏感匹配）
    /// </summary>
    private static readonly string[] DatabaseKeys = ["Database", "Initial Catalog", "InitialCatalog"];

    /// <summary>
    /// SQLite 的库文件字段候选（大小写不敏感匹配）
    /// </summary>
    private static readonly string[] FileKeys = ["Data Source", "DataSource", "FileName", "Filename"];

    /// <summary>
    /// 按约定派生模块库连接串
    /// </summary>
    /// <param name="tenantConnectionString">租户主库连接串（明文）</param>
    /// <param name="dbType">租户主库的数据库类型</param>
    /// <param name="moduleDataSource">模块数据源名</param>
    /// <returns>该租户该模块的库连接串</returns>
    /// <exception cref="NotSupportedException">连接串里找不到可派生的库名字段</exception>
    public static string Derive(string tenantConnectionString, DbType dbType, string moduleDataSource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantConnectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleDataSource);

        var suffix = $"{ModuleDataSourceConfigIds.Separator}{moduleDataSource.Trim()}";
        var builder = new DbConnectionStringBuilder { ConnectionString = tenantConnectionString };

        return dbType == DbType.Sqlite
            ? DeriveFilePath(builder, suffix, dbType, moduleDataSource)
            : DeriveDatabaseName(builder, suffix, dbType, moduleDataSource);
    }

    /// <summary>
    /// 关系库：换库名
    /// </summary>
    private static string DeriveDatabaseName(DbConnectionStringBuilder builder, string suffix, DbType dbType, string moduleDataSource)
    {
        var key = FindKey(builder, DatabaseKeys) ?? throw BuildNotSupported(dbType, moduleDataSource, "库名");
        var database = builder[key]?.ToString();
        if (string.IsNullOrWhiteSpace(database))
        {
            throw BuildNotSupported(dbType, moduleDataSource, "库名");
        }

        builder[key] = $"{database.Trim()}{suffix}";
        return builder.ConnectionString;
    }

    /// <summary>
    /// SQLite：换库文件名，保留原目录与扩展名
    /// </summary>
    private static string DeriveFilePath(DbConnectionStringBuilder builder, string suffix, DbType dbType, string moduleDataSource)
    {
        var key = FindKey(builder, FileKeys) ?? throw BuildNotSupported(dbType, moduleDataSource, "库文件路径");
        var path = builder[key]?.ToString();
        if (string.IsNullOrWhiteSpace(path))
        {
            throw BuildNotSupported(dbType, moduleDataSource, "库文件路径");
        }

        var directory = Path.GetDirectoryName(path);
        var fileName = $"{Path.GetFileNameWithoutExtension(path)}{suffix}{Path.GetExtension(path)}";
        builder[key] = string.IsNullOrEmpty(directory) ? fileName : Path.Combine(directory, fileName);
        return builder.ConnectionString;
    }

    /// <summary>
    /// 在连接串里找出候选字段中实际出现的那一个
    /// </summary>
    private static string? FindKey(DbConnectionStringBuilder builder, string[] candidates)
    {
        return Array.Find(candidates, builder.ContainsKey);
    }

    /// <summary>
    /// 构造「派生不出来」的异常
    /// </summary>
    private static NotSupportedException BuildNotSupported(DbType dbType, string moduleDataSource, string missingPart)
    {
        return new NotSupportedException(
            $"无法为模块数据源 [{moduleDataSource}] 派生租户模块库：{dbType} 的连接串里没有可识别的{missingPart}字段。" +
            $"请由 {nameof(ISqlSugarTenantConnectionProvider)} 显式给出该租户的 ModuleDataSourceConfigs，" +
            $"或关闭 {nameof(Options.XiHanSqlSugarCoreOptions)}.{nameof(Options.XiHanSqlSugarCoreOptions.EnableTenantModuleDatabaseConvention)}。");
    }
}
