// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using System.Data.Common;
using XiHan.Framework.Data.SqlSugar.Tenanting;

namespace XiHan.Framework.Data.Tests;

/// <summary>
/// 租户模块库连接串派生规则测试。
/// </summary>
/// <remarks>
/// 派生只动库名（SQLite 只动库文件名），其余字段必须原样保留——凭据、超时、SSL 这些一旦丢失，
/// 表现是租户模块库连不上或安全参数被悄悄降级，而不是一个显眼的错误。
/// 认不出库名字段时宁可抛：静默回落公共模块库等于把租户数据写进别人的库。
/// </remarks>
public sealed class TenantModuleConnectionStringDeriverTests
{
    [Fact]
    public void PostgreSql_按库名派生()
    {
        var derived = TenantModuleConnectionStringDeriver.Derive(
            "Server=127.0.0.1;Port=5432;Database=qqq;Username=postgres;Password=postgres",
            DbType.PostgreSQL,
            "Erp");

        Assert.Equal("qqq_Erp", ValueOf(derived, "Database"));
    }

    [Fact]
    public void MySql_按库名派生()
    {
        var derived = TenantModuleConnectionStringDeriver.Derive(
            "Server=127.0.0.1;Database=qqq;Uid=root;Pwd=root",
            DbType.MySql,
            "Erp");

        Assert.Equal("qqq_Erp", ValueOf(derived, "Database"));
    }

    [Fact]
    public void SqlServer_认识_InitialCatalog()
    {
        var derived = TenantModuleConnectionStringDeriver.Derive(
            "Server=.;Initial Catalog=qqq;User Id=sa;Password=sa",
            DbType.SqlServer,
            "Erp");

        Assert.Equal("qqq_Erp", ValueOf(derived, "Initial Catalog"));
    }

    [Fact]
    public void Sqlite_换的是库文件名并保留目录与扩展名()
    {
        var derived = TenantModuleConnectionStringDeriver.Derive(
            @"DataSource=C:\data\qqq.db;Pooling=False",
            DbType.Sqlite,
            "Erp");

        Assert.Equal(@"C:\data\qqq_Erp.db", ValueOf(derived, "DataSource"));
        Assert.Equal("False", ValueOf(derived, "Pooling"));
    }

    [Theory]
    [InlineData(@"C:\data\qqq.db", @"C:\data\qqq_Erp.db")]
    [InlineData("/var/lib/xihan/qqq.db", "/var/lib/xihan/qqq_Erp.db")]
    [InlineData("./data/qqq.db", "./data/qqq_Erp.db")]
    [InlineData("qqq.db", "qqq_Erp.db")]
    [InlineData("qqq", "qqq_Erp")]
    [InlineData("qqq.sqlite3", "qqq_Erp.sqlite3")]
    public void Sqlite_派生结果不随运行平台变化(string original, string expected)
    {
        // 连接串是配置数据，不是本地文件系统路径。Path.* 用的是运行平台的语义——
        // Linux 上 \ 不算分隔符，Windows 路径会被整串当文件名；Path.Combine 还会替换分隔符。
        // 这组用例在 Windows 与 Linux 上必须给出同一个答案，否则本地绿、CI 红。
        var derived = TenantModuleConnectionStringDeriver.Derive(
            $"DataSource={original};Pooling=False",
            DbType.Sqlite,
            "Erp");

        Assert.Equal(expected, ValueOf(derived, "DataSource"));
    }

    [Fact]
    public void Sqlite_路径以分隔符结尾时拒绝派生()
    {
        // 没有文件名可换，派生出来的是个目录，连不上——按 fail-closed 拒绝
        _ = Assert.Throws<NotSupportedException>(() => TenantModuleConnectionStringDeriver.Derive(
            "DataSource=/var/lib/xihan/",
            DbType.Sqlite,
            "Erp"));
    }

    [Fact]
    public void 派生只动库名其余字段原样保留()
    {
        var derived = TenantModuleConnectionStringDeriver.Derive(
            "Server=127.0.0.1;Port=5432;Database=qqq;Username=postgres;Password=\"p@ss;word\";Timeout=30",
            DbType.PostgreSQL,
            "Erp");

        Assert.Equal("127.0.0.1", ValueOf(derived, "Server"));
        Assert.Equal("5432", ValueOf(derived, "Port"));
        Assert.Equal("postgres", ValueOf(derived, "Username"));
        // 带分号的口令按连接串规则加了引号，派生后必须仍能完整取回（引号不能被拆掉）
        Assert.Equal("p@ss;word", ValueOf(derived, "Password"));
        Assert.Equal("30", ValueOf(derived, "Timeout"));
    }

    [Fact]
    public void 模块名大小写与派生结果一致()
    {
        var derived = TenantModuleConnectionStringDeriver.Derive(
            "Server=127.0.0.1;Database=qqq;Username=postgres",
            DbType.PostgreSQL,
            " Mes ");

        Assert.Equal("qqq_Mes", ValueOf(derived, "Database"));
    }

    [Fact]
    public void 找不到库名字段时拒绝派生()
    {
        // Oracle 连接串没有库名概念，派生不出来就必须抛，不能静默回落公共模块库
        _ = Assert.Throws<NotSupportedException>(() => TenantModuleConnectionStringDeriver.Derive(
            "Data Source=//127.0.0.1:1521/ORCL;User Id=scott;Password=tiger",
            DbType.Oracle,
            "Erp"));
    }

    [Fact]
    public void 库名为空时拒绝派生()
    {
        _ = Assert.Throws<NotSupportedException>(() => TenantModuleConnectionStringDeriver.Derive(
            "Server=127.0.0.1;Database=;Username=postgres",
            DbType.PostgreSQL,
            "Erp"));
    }

    /// <summary>
    /// 从连接串里按字段名取值。
    /// </summary>
    private static string? ValueOf(string connectionString, string key)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        return builder.TryGetValue(key, out var value) ? value?.ToString() : null;
    }
}
