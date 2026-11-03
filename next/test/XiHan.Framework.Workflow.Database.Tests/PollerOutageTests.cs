// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Database.Polling;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 宿主在服务中而存储被拿走，与先停宿主再拿走存储：轮询器的两种表现。
/// </summary>
/// <remarks>
/// BasicApp 的「存储不可达」用例在完整宿主还在服务时删掉测试库，轮询器去拿领导权，如实报 <c>WORKFLOW_UNAVAILABLE_001</c>；一致性夹具先停宿主（排空、释放领导权、等循环退出）再删库，之后没有告警。前一种是真实的存储故障，不该被压掉；后一种不该有任何告警。
/// </remarks>
public sealed class PollerOutageTests
{
    [Fact]
    public async Task StoreRemovedWhileServing_IsReportedAsUnavailable_OnSqlite()
    {
        var logs = new LogCapture();
        await using var database = new SqliteDatabase();
        var host = await database.StartHostAsync(services: services => services.AddSingleton<ILoggerFactory>(logs));
        var loop = host.Services.GetRequiredService<DueBookmarkPollingLoop>();
        Assert.True(host.App.State.IsServing());

        // 宿主在服务中把租约库换成同名目录（驱动打不开）：周期去拿领导权，真实地在驱动里失败
        await BlockAsync(database.LeasePath);
        try
        {
            Assert.Equal(0, await loop.RunCycleAsync(CancellationToken.None));
            Assert.False(loop.IsLeader);
            Assert.Contains(logs.Entries, static entry => IsLeadershipWarning(entry));
        }
        finally
        {
            await host.DisposeAsync();
            Directory.Delete(database.LeasePath, recursive: true);
        }
    }

    [Fact]
    public async Task StoreRemovedAfterTheHostStopped_LeavesNoWarnings_OnSqlite()
    {
        var logs = new LogCapture();
        await using var database = new SqliteDatabase();
        var host = await database.StartHostAsync(services: services => services.AddSingleton<ILoggerFactory>(logs));
        var loop = host.Services.GetRequiredService<DueBookmarkPollingLoop>();
        Assert.Equal(0, await loop.RunCycleAsync(CancellationToken.None));
        Assert.True(loop.IsLeader);

        // 一致性夹具的释放顺序：先停宿主——排空中释放领导权、等循环退出——再拿走库
        await host.DisposeAsync();
        Assert.False(loop.IsRunning);
        Assert.False(loop.IsLeader);
        await BlockAsync(database.LeasePath);
        Directory.Delete(database.LeasePath, recursive: true);

        Assert.DoesNotContain(logs.Entries, static entry => IsWorkflowWarning(entry));
    }

    [Fact]
    public async Task DatabaseDroppedWhileServing_IsReportedAsUnavailable_ButNotAfterTheHostStopped_OnPostgres()
    {
        // 与 BasicApp 的报回同一形状：PostgreSQL 上 DROP DATABASE … WITH (FORCE)
        var serving = new LogCapture();
        await using (var database = PostgresDatabase.Create())
        {
            var host = await database.StartHostAsync(services: services => services.AddSingleton<ILoggerFactory>(serving));
            var loop = host.Services.GetRequiredService<DueBookmarkPollingLoop>();
            Assert.True(host.App.State.IsServing());
            database.Drop();
            Assert.Equal(0, await loop.RunCycleAsync(CancellationToken.None));
            Assert.False(loop.IsLeader);
        }

        Assert.Contains(serving.Entries, static entry => IsLeadershipWarning(entry));

        var stopped = new LogCapture();
        await using (var database = PostgresDatabase.Create())
        {
            var host = await database.StartHostAsync(services: services => services.AddSingleton<ILoggerFactory>(stopped));
            var loop = host.Services.GetRequiredService<DueBookmarkPollingLoop>();
            Assert.Equal(0, await loop.RunCycleAsync(CancellationToken.None));
            Assert.True(loop.IsLeader);
            await host.DisposeAsync();
            Assert.False(loop.IsRunning);
            database.Drop();
        }

        Assert.DoesNotContain(stopped.Entries, static entry => IsWorkflowWarning(entry));
    }

    /// <summary>
    /// 把库文件换成同名目录，SQLite 驱动打不开它。
    /// </summary>
    private static async Task BlockAsync(string path)
    {
        await SqliteDatabase.DeleteAsync(path);
        Directory.CreateDirectory(path);
    }

    /// <summary>
    /// 领导权以存储不可达失败的告警。
    /// </summary>
    private static bool IsLeadershipWarning((LogLevel Level, string Category, string Message) entry)
        => entry.Level == LogLevel.Warning
            && entry.Category == typeof(PollerLeadership).FullName
            && entry.Message.Contains(WorkflowErrors.StoreUnavailable, StringComparison.Ordinal);

    /// <summary>
    /// 工作流的任何告警或错误。
    /// </summary>
    private static bool IsWorkflowWarning((LogLevel Level, string Category, string Message) entry)
        => entry.Level >= LogLevel.Warning && entry.Category.StartsWith("XiHan.Framework.Workflow", StringComparison.Ordinal);
}
