// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Persistence.EntityFrameworkCore;
using XiHan.Framework.Testing;
using XiHan.Framework.Workflow.Abstractions;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 每个测试一组临时 SQLite 文件，宿主装配 EF Core 持久化与数据库工作流
/// </summary>
/// <remarks>
/// DbContext 是 <see cref="WorkflowDbContext"/>
/// 与 <see cref="SqliteDatabase"/> 同理：SQLite 只有一个写者，租约表要放到另一个库文件——EF Core 以 <c>XiHan:Persistence:EntityFrameworkCore:Connections</c> 声明命名连接 <see cref="LeaseConnection"/>，<see cref="DatabaseWorkflowOptions.LockConnection"/> 指向它；租约的每一步开绑定这个连接的工作单元，作用域里的 DbContext 按租约库的连接串建出。<c>EnsureCreated</c> 在默认库与租约库上各建一次表。释放时停止宿主并删除全部文件
/// </remarks>
internal sealed class EntityFrameworkCoreSqliteDatabase : IAsyncDisposable
{
    /// <summary>
    /// 租约表所在的命名连接
    /// </summary>
    public const string LeaseConnection = "leases";

    /// <summary>
    /// 已启动的宿主
    /// </summary>
    /// <remarks>
    /// 释放时一并停止
    /// </remarks>
    private TestHost? _host;

    /// <summary>
    /// 业务库文件的完整路径
    /// </summary>
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"xihan-workflow-efcore-{Guid.NewGuid():N}.db");

    /// <summary>
    /// 租约库文件的完整路径
    /// </summary>
    public string LeasePath => Path + ".leases.db";

    /// <summary>
    /// 宿主配置
    /// </summary>
    /// <remarks>
    /// 业务库绑定 <c>db</c>，租约库声明为命名连接并指给租约表；开启 <c>EnsureCreated</c>、关掉后台轮询（周期 1 小时）、实例锁等待上限 2 秒
    /// </remarks>
    public IEnumerable<KeyValuePair<string, string?>> Configuration =>
    [
        new("XiHan:Bindings:db", SqliteDatabase.Connection(Path)),
        new($"{EntityFrameworkCorePersistenceOptions.ConfigurationSection}:Connections:{LeaseConnection}", SqliteDatabase.Connection(LeasePath)),
        new($"{DatabaseWorkflowOptions.ConfigurationSection}:LockConnection", LeaseConnection),
        new($"{EntityFrameworkCorePersistenceOptions.ConfigurationSection}:EnsureCreated", "true"),
        new($"{DatabaseWorkflowOptions.ConfigurationSection}:PollIntervalSeconds", "3600"),
        new($"{WorkflowOptions.ConfigurationSection}:LockTimeoutSeconds", "2"),
    ];

    /// <summary>
    /// 启动装配了本数据库、EF Core 持久化与 <see cref="DatabaseWorkflowFeature"/> 的宿主
    /// </summary>
    /// <remarks>
    /// 后给的配置覆盖默认配置；值为 <see langword="null"/> 的键从默认配置里去掉
    /// </remarks>
    public async Task<TestHost> StartHostAsync(IEnumerable<KeyValuePair<string, string?>>? configuration = null, Action<IServiceCollection>? services = null)
    {
        var settings = Configuration.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in configuration ?? [])
        {
            if (pair.Value is null)
            {
                settings.Remove(pair.Key);
            }
            else
            {
                settings[pair.Key] = pair.Value;
            }
        }

        _host = await TestHost.StartAsync(
            static b => b.UseFeature(new EntityFrameworkCorePersistenceFeature<WorkflowDbContext>(static (binding, options) => options.UseSqlite(binding.ValueOrThrow().Value))).UseFeature<DatabaseWorkflowFeature>(),
            settings,
            services);
        return _host;
    }

    /// <summary>
    /// 停止宿主，清空两个库的连接池并删除库文件及其附属文件
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
            _host = null;
        }

        // 两个库文件的连接串都是 SqliteDatabase.Connection 给出的：按它清各自的连接池再删
        foreach (var file in new[] { Path, LeasePath })
        {
            await SqliteDatabase.DeleteAsync(file);
        }
    }
}
