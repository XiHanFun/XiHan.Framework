// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;
using XiHan.Framework.Kernel.Hosting;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 测试独占的一个 SQL Server 库（由 <see cref="SqlServerTestServer.CreateDatabase"/> 建出），释放时删除。
/// </summary>
internal sealed class SqlServerDatabase(string server, string name) : IDisposable
{
    /// <summary>
    /// 库名。
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// 指向该库的连接串。
    /// </summary>
    public string ConnectionString => $"{server};Database={Name}";

    /// <summary>
    /// 宿主配置：连接绑定 <c>db</c> 与开启 <c>InitTables</c> 建表。
    /// </summary>
    public IEnumerable<KeyValuePair<string, string?>> Configuration =>
    [
        new("XiHan:Bindings:db", ConnectionString),
        new("XiHan:Persistence:SqlSugar:InitTables", "true"),
    ];

    /// <summary>
    /// 创建以 SQL Server 装配的 SqlSugar 特性。
    /// </summary>
    /// <remarks>
    /// 连接串来自宿主解析的绑定。
    /// </remarks>
    public static IFeature CreateFeature()
        => new SqlSugarPersistenceFeature(static (binding, config) =>
        {
            config.DbType = DbType.SqlServer;
            config.ConnectionString = binding.ValueOrThrow().Value;
        });

    /// <summary>
    /// 断开连接池并删库（先切到单用户模式终止其他会话）。
    /// </summary>
    public void Dispose()
    {
        SqlServerTestServer.ClearPools();
        using var maintenance = SqlServerTestServer.Maintenance(server);
        maintenance.Ado.ExecuteCommand($"IF DB_ID(N'{Name}') IS NOT NULL BEGIN ALTER DATABASE [{Name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{Name}]; END");
    }
}
