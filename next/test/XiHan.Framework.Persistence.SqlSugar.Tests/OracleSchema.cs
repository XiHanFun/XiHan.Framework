// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;
using XiHan.Framework.Kernel.Hosting;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 测试独占的一个 Oracle 用户（由 <see cref="OracleTestServer.CreateSchema"/> 建出），释放时连同它的表删除
/// </summary>
internal sealed class OracleSchema(string admin, string user, string connectionString) : IDisposable
{
    /// <summary>
    /// 用户名（即 schema）
    /// </summary>
    public string User { get; } = user;

    /// <summary>
    /// 以该用户登录的连接串
    /// </summary>
    public string ConnectionString { get; } = connectionString;

    /// <summary>
    /// 宿主配置：连接绑定 <c>db</c> 与开启 <c>InitTables</c> 建表
    /// </summary>
    public IEnumerable<KeyValuePair<string, string?>> Configuration =>
    [
        new("XiHan:Bindings:db", ConnectionString),
        new("XiHan:Persistence:SqlSugar:InitTables", "true"),
    ];

    /// <summary>
    /// 创建以 Oracle 装配的 SqlSugar 特性
    /// </summary>
    /// <remarks>
    /// 连接串来自宿主解析的绑定
    /// </remarks>
    public static IFeature CreateFeature()
        => new SqlSugarPersistenceFeature(static (binding, config) =>
        {
            config.DbType = DbType.Oracle;
            config.ConnectionString = binding.ValueOrThrow().Value;
        });

    /// <summary>
    /// 结束它的会话并删用户
    /// </summary>
    /// <remarks>
    /// 用户的连接串不用连接池；并行的用例与后台探针仍可能留着会话，先以管理账号结束它们，DROP USER 才不会因 ORA-01940 失败
    /// </remarks>
    public void Dispose()
    {
        OracleTestServer.ClearPools();
        using var maintenance = OracleTestServer.Maintenance(admin);
        foreach (var session in maintenance.Ado.SqlQuery<string>("SELECT sid || ',' || serial# FROM v$session WHERE username = :u", new SugarParameter(":u", User)))
        {
            maintenance.Ado.ExecuteCommand($"ALTER SYSTEM KILL SESSION '{session}' IMMEDIATE");
        }

        maintenance.Ado.ExecuteCommand($"DROP USER {User} CASCADE");
    }
}
