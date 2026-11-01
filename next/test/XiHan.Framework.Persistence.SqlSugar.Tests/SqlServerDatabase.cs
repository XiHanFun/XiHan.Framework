// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

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
    /// 断开连接池并删库（先切到单用户模式终止其他会话）。
    /// </summary>
    public void Dispose()
    {
        SqlServerTestServer.ClearPools();
        using var maintenance = SqlServerTestServer.Maintenance(server);
        maintenance.Ado.ExecuteCommand($"IF DB_ID(N'{Name}') IS NOT NULL BEGIN ALTER DATABASE [{Name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{Name}]; END");
    }
}
