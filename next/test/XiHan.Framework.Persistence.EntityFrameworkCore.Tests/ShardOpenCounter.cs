// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// 数一个 SQLite 库文件上 EF Core 打开连接的次数。
/// </summary>
/// <remarks>
/// 处理器不碰库的调用里，分片上的读写连接只由工作单元开始时的时间列核对打开（核对打开一次连接读表结构与抽样；问库在不在时 EF Core 另开的只读连接不数），打开次数就是核对次数。给了 <see cref="Hold"/> 时，第一次打开之后挂起到用例放行：核对在这期间持有那个分片的排队门。
/// </remarks>
/// <param name="path">要数的库文件。</param>
internal sealed class ShardOpenCounter(string path) : DbConnectionInterceptor
{
    /// <summary>
    /// 打开次数。
    /// </summary>
    private int _opens;

    /// <summary>
    /// 打开次数。
    /// </summary>
    public int Opens => Volatile.Read(ref _opens);

    /// <summary>
    /// 第一次打开之后挂起到它完成。
    /// </summary>
    /// <remarks>
    /// 为空时不挂起。
    /// </remarks>
    public TaskCompletionSource? Hold { get; init; }

    /// <summary>
    /// 第一次打开已经发生、正挂起。
    /// </summary>
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc />
    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (IsShard(connection)
            && Interlocked.Increment(ref _opens) == 1
            && Hold is { } hold)
        {
            Entered.SetResult();
            await hold.Task.ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (IsShard(connection))
        {
            Interlocked.Increment(ref _opens);
        }
    }

    /// <summary>
    /// 连接是否指向要数的库文件。
    /// </summary>
    /// <remarks>
    /// 按连接串里的数据源比较，不看打开之后驱动报告的路径；只读连接不算。
    /// </remarks>
    private bool IsShard(DbConnection connection)
    {
        // EF Core 问库在不在（IRelationalDatabaseCreator.Exists）时另开一个只读、不入池的连接试开，不是核对读库的那一次，不数
        var options = new SqliteConnectionStringBuilder(connection.ConnectionString);
        return options.Mode != SqliteOpenMode.ReadOnly && string.Equals(options.DataSource, path, StringComparison.OrdinalIgnoreCase);
    }
}
