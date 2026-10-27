// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;
using XiHan.Framework.Kernel.Hosting;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 测试独占的一个 PostgreSQL 库（由 <see cref="PostgresTestServer.CreateDatabase"/> 建出），释放时删除。
/// </summary>
internal sealed class PostgresDatabase(string server, string name) : IDisposable
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
    /// 创建以 PostgreSQL 装配的 SqlSugar 特性；连接串来自宿主解析的绑定。
    /// </summary>
    public static IFeature CreateFeature()
        => new SqlSugarPersistenceFeature(static (binding, config) =>
        {
            config.DbType = DbType.PostgreSQL;
            config.ConnectionString = binding.ValueOrThrow().Value;
        });

    /// <summary>
    /// 断开连接池并删库（<c>WITH (FORCE)</c> 终止其他会话）。
    /// </summary>
    public void Dispose()
    {
        PostgresTestServer.ClearPools();
        using var maintenance = PostgresTestServer.Maintenance(server);
        maintenance.Ado.ExecuteCommand($"DROP DATABASE IF EXISTS \"{Name}\" WITH (FORCE)");
    }
}
