// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Data.Common;
using SqlSugar;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// 走一次数据库往返来探测连通性
/// </summary>
/// <remarks>
/// 打开连接之后执行一条语句。只打开连接不够：连接池把空闲连接直接交出来，数据库宕了也打得开，<c>Ado.IsValidConnection</c> 同样，而且它吞掉原因、不接受取消令牌（ADR-0123）
/// </remarks>
internal static class SqlSugarRoundTrip
{
    /// <summary>
    /// 走一次往返，失败时抛出驱动的异常
    /// </summary>
    /// <remarks>
    /// 令牌取消时抛出 <see cref="OperationCanceledException"/>，驱动不理令牌时也一样：那次探测留在线程池上，到驱动自己的超时才结束
    /// </remarks>
    /// <param name="config">连接配置，只读取</param>
    /// <param name="cancellationToken">超时与取消都经它</param>
    public static async Task RunAsync(ConnectionConfig config, CancellationToken cancellationToken)
    {
        // 不是每个驱动都实现了真正的异步：基类的 OpenAsync 同步打开，只在进入时看一眼令牌。放到线程池上再按令牌等，超时才对所有驱动成立
        var probe = Task.Run(() => ExecuteAsync(config, cancellationToken), cancellationToken);
        try
        {
            await probe.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!probe.IsCompleted)
        {
            // 不再等它：它之后失败也没有人要这个异常，这里取走，免得成为未观察的任务异常
            _ = probe.ContinueWith(static task => _ = task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            throw;
        }
    }

    /// <summary>
    /// 探测用的语句
    /// </summary>
    /// <returns>语句；这种数据库没有通用的语句时为 <see langword="null"/></returns>
    private static string? Statement(DbType dbType) => dbType switch
    {
        DbType.Oracle or DbType.Dm or DbType.OceanBaseForOracle or DbType.TDSQLForOracleODBC => "SELECT 1 FROM DUAL",
        DbType.HANA => "SELECT 1 FROM DUMMY",
        DbType.DB2 => "SELECT 1 FROM SYSIBM.SYSDUMMY1",
        // 不知道后端说什么语言：一条不认识的语句会让健康的库报不可用，只打开连接
        DbType.MongoDb or DbType.Custom or DbType.Odbc => null,
        _ => "SELECT 1",
    };

    /// <summary>
    /// 另建客户端，打开连接并执行语句
    /// </summary>
    /// <remarks>
    /// 不碰调用方的客户端：它可能开着事务，超时后这里还可能在跑，不能和作用域的释放抢同一个连接
    /// </remarks>
    private static async Task ExecuteAsync(ConnectionConfig config, CancellationToken cancellationToken)
    {
        using var client = new SqlSugarClient(config);
        var statement = Statement(config.DbType);
        var connection = client.Ado.Connection;
        if (connection is DbConnection database)
        {
            await database.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (statement is not null)
            {
                await using var command = database.CreateCommand();
                command.CommandText = statement;
                await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            }

            await database.CloseAsync().ConfigureAwait(false);
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        connection.Open();
        if (statement is not null)
        {
            using var command = connection.CreateCommand();
            command.CommandText = statement;
            command.ExecuteScalar();
        }

        connection.Close();
    }
}
