// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;
using XiHan.Framework.Kernel.Hosting;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 测试独占的一个 Oracle 用户（由 <see cref="OracleTestServer.CreateSchema()"/> 建出），释放时连同它的表删除
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
    /// 锁住账户、结束它的会话并删用户
    /// </summary>
    /// <remarks>
    /// 先锁账户再清池：清池之后 ODP.NET 会给这个用户补建连接，锁住之后就建不进来（ADR-0352）。清池关掉闲着的连接，剩下的再以管理账号结束并等它们消失（<see cref="OracleTestServer.EndSessions"/>），DROP USER 才不会因 ORA-01940 失败
    /// </remarks>
    public void Dispose()
    {
        using var maintenance = OracleTestServer.Maintenance(admin);
        // 不先锁：清池后补建的连接正在登录时被杀，回 ORA-00031、要过几秒才从 v$session 消失，CI 上超过等待上限（ADR-0352）
        maintenance.Ado.ExecuteCommand($"ALTER USER {User} ACCOUNT LOCK");
        OracleTestServer.ClearPools();
        OracleTestServer.EndSessions(maintenance, User);
        maintenance.Ado.ExecuteCommand($"DROP USER {User} CASCADE");
    }
}
