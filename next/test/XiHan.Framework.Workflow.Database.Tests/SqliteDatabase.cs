// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.SqlSugar;
using XiHan.Framework.Testing;
using XiHan.Framework.Workflow.Abstractions;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 每个测试一个临时 SQLite 文件，宿主装配 SqlSugar 持久化与数据库工作流特性。
/// </summary>
/// <remarks>
/// 释放时停止宿主并删除文件。
/// </remarks>
internal sealed class SqliteDatabase : IAsyncDisposable
{
    /// <summary>
    /// 已启动的宿主。
    /// </summary>
    /// <remarks>
    /// 释放时一并停止。
    /// </remarks>
    private TestHost? _host;

    /// <summary>
    /// 数据库文件的完整路径。
    /// </summary>
    /// <remarks>
    /// 位于系统临时目录，文件名带随机后缀以免测试之间互相干扰。
    /// </remarks>
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"xihan-workflow-{Guid.NewGuid():N}.db");

    /// <summary>
    /// 指向该文件的 SQLite 连接串。
    /// </summary>
    public string ConnectionString => $"Data Source={Path}";

    /// <summary>
    /// 宿主配置。
    /// </summary>
    /// <remarks>
    /// 连接绑定 <c>db</c>、开启 <c>InitTables</c> 建表、关掉后台轮询（周期 1 小时）、实例锁等待上限 0.2 秒。
    /// </remarks>
    public IEnumerable<KeyValuePair<string, string?>> Configuration =>
    [
        new("XiHan:Bindings:db", ConnectionString),
        new("XiHan:Persistence:SqlSugar:InitTables", "true"),
        new($"{DatabaseWorkflowOptions.ConfigurationSection}:PollIntervalSeconds", "3600"),
        new($"{WorkflowOptions.ConfigurationSection}:LockTimeoutSeconds", "0.2"),
    ];

    /// <summary>
    /// 创建以 SQLite 装配的 SqlSugar 特性。
    /// </summary>
    /// <remarks>
    /// 连接串来自宿主解析的绑定。
    /// </remarks>
    public static IFeature CreateFeature()
        => new SqlSugarPersistenceFeature(static (binding, config) =>
        {
            config.DbType = DbType.Sqlite;
            config.ConnectionString = binding.ValueOrThrow().Value;
        });

    /// <summary>
    /// 启动装配了本数据库与 <see cref="DatabaseWorkflowFeature"/> 的宿主。
    /// </summary>
    /// <remarks>
    /// 后给的配置覆盖默认配置。
    /// </remarks>
    public async Task<TestHost> StartHostAsync(IEnumerable<KeyValuePair<string, string?>>? configuration = null, Action<IServiceCollection>? services = null)
    {
        // 内存配置不接受重复键：后给的覆盖默认的
        var settings = Configuration.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in configuration ?? [])
        {
            settings[pair.Key] = pair.Value;
        }

        _host = await TestHost.StartAsync(static b => b.UseFeature(CreateFeature()).UseFeature<DatabaseWorkflowFeature>(), settings, services);
        return _host;
    }

    /// <summary>
    /// 停止宿主，清空连接池并删除数据库文件及其日志、WAL 与共享内存文件。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
            _host = null;
        }

        // SQLite 连接会被池化并持有文件句柄，先清空连接池，Windows 上才能删除文件
        SqliteConnection.ClearAllPools();
        File.Delete(Path);
        File.Delete(Path + "-journal");
        File.Delete(Path + "-wal");
        File.Delete(Path + "-shm");
    }
}
