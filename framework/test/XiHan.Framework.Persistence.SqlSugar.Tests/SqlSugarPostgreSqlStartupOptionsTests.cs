// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Data.Common;
using SqlSugar;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 约定给 PostgreSQL 连接串加上字面量规则的启动参数（ADR-0100）
/// </summary>
/// <remarks>
/// 启动参数是 <c>-c standard_conforming_strings=on</c>。不连库，只看 <see cref="SqlSugarConventions.Apply"/> 之后的连接串：已有的启动参数保留、已写 on 的不重复加、写了 off 的装配即失败（不静默改成 on），读写分离的从库连接一样处理，别的数据库不动
/// </remarks>
public sealed class SqlSugarPostgreSqlStartupOptionsTests
{
    /// <summary>
    /// 测试用的服务器连接串（带口令，核对异常消息不泄漏）
    /// </summary>
    private const string Server = "Host=127.0.0.1;Port=5432;Database=app;Username=app;Password=Secret_Pwd_1";

    [Fact]
    public void Apply_AddsTheStartupOption_AndKeepsTheRestOfTheConnectionString()
    {
        var config = SqlSugarConventions.Apply(new ConnectionConfig { DbType = DbType.PostgreSQL, ConnectionString = Server });

        var builder = new DbConnectionStringBuilder { ConnectionString = config.ConnectionString };
        Assert.Equal("-c standard_conforming_strings=on", builder["Options"]);
        Assert.Equal("Secret_Pwd_1", builder["Password"]);
        Assert.Equal("app", builder["Database"]);
    }

    [Fact]
    public void Apply_AppendsToExistingOptions_AndDoesNotRepeatItself()
    {
        var config = SqlSugarConventions.Apply(SqlSugarConventions.Apply(new ConnectionConfig { DbType = DbType.PostgreSQL, ConnectionString = $"{Server};Options=-c search_path=app" }));

        Assert.Equal("-c search_path=app -c standard_conforming_strings=on", new DbConnectionStringBuilder { ConnectionString = config.ConnectionString }["Options"]);
    }

    [Theory]
    [InlineData("-c standard_conforming_strings=on")]
    [InlineData("-c standard_conforming_strings=true")]
    [InlineData("--standard_conforming_strings=1")]
    [InlineData("-cstandard_conforming_strings=ON")]
    public void Apply_KeepsAnExplicitOn(string options)
    {
        var config = SqlSugarConventions.Apply(new ConnectionConfig { DbType = DbType.PostgreSQL, ConnectionString = $"{Server};Options={options}" });

        Assert.Equal(options, new DbConnectionStringBuilder { ConnectionString = config.ConnectionString }["Options"]);
    }

    [Theory]
    [InlineData("-c standard_conforming_strings=off")]
    [InlineData("-c search_path=app --standard_conforming_strings=false")]
    [InlineData("-c standard_conforming_strings=0")]
    public void Apply_RejectsAnExplicitOff_WithoutLeakingTheConnectionString(string options)
    {
        var error = Assert.Throws<ArgumentException>(() => SqlSugarConventions.Apply(new ConnectionConfig { DbType = DbType.PostgreSQL, ConnectionString = $"{Server};Options={options}" }));

        Assert.Contains("standard_conforming_strings", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Secret_Pwd_1", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_CoversReadReplicaConnections()
    {
        var config = SqlSugarConventions.Apply(new ConnectionConfig
        {
            DbType = DbType.PostgreSQL,
            ConnectionString = Server,
            SlaveConnectionConfigs = [new SlaveConnectionConfig { ConnectionString = Server.Replace("127.0.0.1", "127.0.0.2", StringComparison.Ordinal), HitRate = 10 }],
        });

        Assert.Equal("-c standard_conforming_strings=on", new DbConnectionStringBuilder { ConnectionString = config.SlaveConnectionConfigs[0].ConnectionString }["Options"]);
    }

    [Theory]
    [InlineData(DbType.MySql, "Server=127.0.0.1;Database=app;User Id=app;Password=p")]
    [InlineData(DbType.SqlServer, "Server=127.0.0.1;Database=app;User Id=app;Password=p")]
    [InlineData(DbType.Sqlite, "Data Source=app.db")]
    [InlineData(DbType.Kdbndp, "Server=127.0.0.1;Database=app;User Id=app;Password=p")]
    public void Apply_LeavesOtherDatabasesAlone(DbType dbType, string connectionString)
    {
        Assert.Equal(connectionString, SqlSugarConventions.Apply(new ConnectionConfig { DbType = dbType, ConnectionString = connectionString }).ConnectionString);
    }
}
