// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Tasks.SqlSugar.BackgroundJobs;
using XiHan.Framework.Tasks.SqlSugar.Clients;
using XiHan.Framework.Tasks.SqlSugar.Entities;
using XiHan.Framework.Tasks.SqlSugar.Options;
using XiHan.Framework.Tasks.SqlSugar.ScheduledJobs;

namespace XiHan.Framework.Tasks.SqlSugar.Tests;

/// <summary>
/// 任务存储测试夹具，提供一个临时 SQLite 库与被测存储
/// </summary>
internal sealed class TasksTestContext : IDisposable
{
    private readonly string _databaseFile;
    private readonly ServiceProvider _serviceProvider;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="leaseTimeout">后台作业租约时长，默认五分钟</param>
    /// <param name="maxClaimBatchSize">单次领取的批量上限，默认取配置默认值</param>
    public TasksTestContext(TimeSpan? leaseTimeout = null, int? maxClaimBatchSize = null)
    {
        _databaseFile = Path.Combine(Path.GetTempPath(), $"xihan_tasks_{Guid.NewGuid():N}.db");

        Client = new SqlSugarClient(new ConnectionConfig
        {
            // 关闭连接池，用例结束后驱动不再持有临时库文件句柄
            ConnectionString = $"DataSource={_databaseFile};Pooling=False",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true
        });
        Client.CodeFirst.InitTables(typeof(SysBackgroundJob), typeof(SysJobInstance), typeof(SysJobHistory));

        Tenant = new CurrentTenant(AsyncLocalCurrentTenantAccessor.Instance);
        Client.Aop.DataExecuting = (_, _) => ExecutingTenantIds.Add(Tenant.Id);

        Resolver = new StubClientResolver(Client, Tenant);

        var services = new ServiceCollection();
        services.AddScoped<ISqlSugarClientResolver>(_ => Resolver);
        _serviceProvider = services.BuildServiceProvider();

        Accessor = new TasksHostClientAccessor(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Tenant);

        Clock = new FakeClock(BaseTime);

        var optionValues = new XiHanTasksSqlSugarOptions
        {
            BackgroundJobLeaseTimeout = leaseTimeout ?? TimeSpan.FromMinutes(5),
            RunningInstanceGracePeriod = TimeSpan.FromMinutes(1)
        };
        if (maxClaimBatchSize.HasValue)
        {
            optionValues.MaxClaimBatchSize = maxClaimBatchSize.Value;
        }

        var options = Microsoft.Extensions.Options.Options.Create(optionValues);

        BackgroundJobStore = new SqlSugarBackgroundJobStore(Accessor, Clock, options);
        JobStore = new SqlSugarJobStore(Accessor, options);
    }

    /// <summary>
    /// 用例的基准时间，远离真实的当前时间
    /// </summary>
    public static DateTime BaseTime { get; } = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// 临时库客户端
    /// </summary>
    public SqlSugarClient Client { get; }

    /// <summary>
    /// 当前租户
    /// </summary>
    public ICurrentTenant Tenant { get; }

    /// <summary>
    /// 记录解析时租户的客户端解析器
    /// </summary>
    public StubClientResolver Resolver { get; }

    /// <summary>
    /// 宿主上下文客户端访问器
    /// </summary>
    public TasksHostClientAccessor Accessor { get; }

    /// <summary>
    /// 可控时钟
    /// </summary>
    public FakeClock Clock { get; }

    /// <summary>
    /// 被测后台作业存储
    /// </summary>
    public SqlSugarBackgroundJobStore BackgroundJobStore { get; }

    /// <summary>
    /// 被测定时任务存储
    /// </summary>
    public SqlSugarJobStore JobStore { get; }

    /// <summary>
    /// 每次触发数据执行事件时的租户标识
    /// </summary>
    public List<long?> ExecutingTenantIds { get; } = [];

    /// <summary>
    /// 释放客户端并删除临时库文件
    /// </summary>
    public void Dispose()
    {
        _serviceProvider.Dispose();
        Client.Dispose();

        if (File.Exists(_databaseFile))
        {
            File.Delete(_databaseFile);
        }
    }
}
