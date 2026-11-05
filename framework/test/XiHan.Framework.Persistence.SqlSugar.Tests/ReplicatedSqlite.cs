// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Data.Sqlite;
using SqlSugar;
using XiHan.Framework.Kernel.Hosting;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 用几个 SQLite 文件代替一主多从
/// </summary>
/// <remarks>
/// SQLite 没有复制，几个文件互不相干。每个文件的 <c>marker</c> 表写着它自己的名字，读到哪个名字就知道读的是哪个库；主库的写入不会出现在从库上，读不到自己刚写的行就说明读走了从库。验证的是路由与健康，真实的复制由 <c>PostgresReplicaTests</c> 验证
/// </remarks>
internal sealed class ReplicatedSqlite : IDisposable
{
    /// <summary>
    /// 读 <c>marker</c> 第一行的语句
    /// </summary>
    public const string ReadMarker = "select name from marker order by rowid limit 1";

    /// <summary>
    /// 创建主库与两个从库，各写上自己的名字
    /// </summary>
    public ReplicatedSqlite()
    {
        Seed(Primary, "primary");
        Seed(ReplicaA, "replica-a");
        Seed(ReplicaB, "replica-b");
    }

    /// <summary>
    /// 主库
    /// </summary>
    public SqliteDatabase Primary { get; } = new();

    /// <summary>
    /// 第一个从库
    /// </summary>
    public SqliteDatabase ReplicaA { get; } = new();

    /// <summary>
    /// 第二个从库
    /// </summary>
    public SqliteDatabase ReplicaB { get; } = new();

    /// <summary>
    /// 宿主配置：连接绑定 <c>db</c> 指向主库
    /// </summary>
    public IEnumerable<KeyValuePair<string, string?>> Configuration => [new("XiHan:Bindings:db", Primary.ConnectionString)];

    /// <summary>
    /// 在库里建 <c>marker</c> 表并写上名字
    /// </summary>
    public static void Seed(SqliteDatabase database, string name) => Seed(database.ConnectionString, name);

    /// <summary>
    /// 在连接串指向的库里建 <c>marker</c> 表并写上名字
    /// </summary>
    public static void Seed(string connectionString, string name)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "create table marker(name text); insert into marker(name) values ($name)";
        command.Parameters.AddWithValue("$name", name);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// 库里 <c>marker</c> 的全部行
    /// </summary>
    /// <remarks>
    /// 绕过 SqlSugar 直接读文件，不受路由影响
    /// </remarks>
    public static List<string> Markers(SqliteDatabase database)
    {
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select name from marker order by rowid";
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    /// <summary>
    /// 建配了从库的特性
    /// </summary>
    /// <param name="replicas">从库的连接串与权重</param>
    /// <param name="configure">之后再改连接配置（如挂 AOP）</param>
    public static IFeature CreateFeature(IEnumerable<(string ConnectionString, int HitRate)> replicas, Action<ConnectionConfig>? configure = null)
    {
        var list = replicas.ToList();
        return new SqlSugarPersistenceFeature((binding, config) =>
        {
            config.DbType = DbType.Sqlite;
            config.ConnectionString = binding.ValueOrThrow().Value;
            config.SlaveConnectionConfigs = [.. list.Select(static replica => new SlaveConnectionConfig { ConnectionString = replica.ConnectionString, HitRate = replica.HitRate })];
            configure?.Invoke(config);
        });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Primary.Dispose();
        ReplicaA.Dispose();
        ReplicaB.Dispose();
    }
}
