// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SqlSugar;
using XiHan.Framework.Auditing.SqlSugar.Entities;
using XiHan.Framework.Auditing.SqlSugar.Extensions.DependencyInjection;
using XiHan.Framework.Auditing.SqlSugar.Writers;
using XiHan.Framework.Auditing.Writers;
using XiHan.Framework.Data.SqlSugar.Auditing;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Auditing.SqlSugar.Tests;

/// <summary>
/// 日志写入器测试
/// </summary>
public class LogWriterTests
{
    private const long AmbientTenantId = 1001L;
    private const long RecordTenantId = 2002L;

    [Fact]
    public async Task 访问日志写入器按实体类型路由并落入当月分表()
    {
        var databaseFile = NewDatabasePath();

        try
        {
            using var db = CreateClient(databaseFile);

            db.CodeFirst.SplitTables().InitTables(typeof(SysAccessLog));

            var tenant = new RecordingCurrentTenant(AmbientTenantId);
            var resolver = new StubClientResolver(db, () => tenant.Id);
            var writer = new SqlSugarAccessLogWriter(
                resolver,
                IdGeneratorFactory.CreateSnowflakeIdGenerator_LowWorkload(),
                tenant);

            var before = DateTimeOffset.UtcNow;
            await writer.WriteAsync(new AccessLogRecord
            {
                TenantId = RecordTenantId,
                TraceId = "trace-access",
                Method = "GET",
                Path = "/Home",
                QueryString = "?page=1"
            });
            var after = DateTimeOffset.UtcNow;

            AssertRouted(resolver, typeof(SysAccessLog));
            AssertWrittenInRecordTenant(resolver, tenant);

            var range = CurrentUtcMonthRange();
            var found = db.Queryable<SysAccessLog>()
                .SplitTable(range[0], range[1])
                .Where(item => item.TraceId == "trace-access")
                .ToList();

            var row = Assert.Single(found);
            Assert.Equal(RecordTenantId, row.TenantId);
            Assert.NotEqual(0L, row.BasicId);
            AssertCreatedTimeNearNow(row.CreatedTime, before, after);
        }
        finally
        {
            DeleteDatabase(databaseFile);
        }
    }

    [Fact]
    public async Task 接口日志写入器按实体类型路由并落入当月分表()
    {
        var databaseFile = NewDatabasePath();

        try
        {
            using var db = CreateClient(databaseFile);

            db.CodeFirst.SplitTables().InitTables(typeof(SysApiLog));

            var tenant = new RecordingCurrentTenant(AmbientTenantId);
            var resolver = new StubClientResolver(db, () => tenant.Id);
            var writer = new SqlSugarApiLogWriter(
                resolver,
                IdGeneratorFactory.CreateSnowflakeIdGenerator_LowWorkload(),
                tenant);

            var before = DateTimeOffset.UtcNow;
            await writer.WriteAsync(new ApiLogRecord
            {
                TenantId = RecordTenantId,
                TraceId = "trace-api",
                Method = "POST",
                Path = "/Api/Order",
                AppId = "app-1",
                StatusCode = 200
            });
            var after = DateTimeOffset.UtcNow;

            AssertRouted(resolver, typeof(SysApiLog));
            AssertWrittenInRecordTenant(resolver, tenant);

            var range = CurrentUtcMonthRange();
            var found = db.Queryable<SysApiLog>()
                .SplitTable(range[0], range[1])
                .Where(item => item.TraceId == "trace-api")
                .ToList();

            var row = Assert.Single(found);
            Assert.Equal(RecordTenantId, row.TenantId);
            Assert.NotEqual(0L, row.BasicId);
            Assert.Equal("app-1", row.AppId);
            AssertCreatedTimeNearNow(row.CreatedTime, before, after);
        }
        finally
        {
            DeleteDatabase(databaseFile);
        }
    }

    [Fact]
    public async Task 异常日志写入器按实体类型路由并落入当月分表()
    {
        var databaseFile = NewDatabasePath();

        try
        {
            using var db = CreateClient(databaseFile);

            db.CodeFirst.SplitTables().InitTables(typeof(SysExceptionLog));

            var tenant = new RecordingCurrentTenant(AmbientTenantId);
            var resolver = new StubClientResolver(db, () => tenant.Id);
            var writer = new SqlSugarExceptionLogWriter(
                resolver,
                IdGeneratorFactory.CreateSnowflakeIdGenerator_LowWorkload(),
                tenant);

            var before = DateTimeOffset.UtcNow;
            await writer.WriteAsync(new ExceptionLogRecord
            {
                TenantId = RecordTenantId,
                TraceId = "trace-exception",
                Method = "GET",
                Path = "/Boom",
                StatusCode = 500,
                ExceptionType = typeof(InvalidOperationException).FullName!,
                ExceptionMessage = "boom"
            });
            var after = DateTimeOffset.UtcNow;

            AssertRouted(resolver, typeof(SysExceptionLog));
            AssertWrittenInRecordTenant(resolver, tenant);

            var range = CurrentUtcMonthRange();
            var found = db.Queryable<SysExceptionLog>()
                .SplitTable(range[0], range[1])
                .Where(item => item.TraceId == "trace-exception")
                .ToList();

            var row = Assert.Single(found);
            Assert.Equal(RecordTenantId, row.TenantId);
            Assert.NotEqual(0L, row.BasicId);
            Assert.Equal(500, row.StatusCode);
            AssertCreatedTimeNearNow(row.CreatedTime, before, after);
        }
        finally
        {
            DeleteDatabase(databaseFile);
        }
    }

    [Fact]
    public async Task 登录日志写入器按实体类型路由并落入当月分表()
    {
        var databaseFile = NewDatabasePath();

        try
        {
            using var db = CreateClient(databaseFile);

            db.CodeFirst.SplitTables().InitTables(typeof(SysLoginLog));

            var tenant = new RecordingCurrentTenant(AmbientTenantId);
            var resolver = new StubClientResolver(db, () => tenant.Id);
            var writer = new SqlSugarLoginLogWriter(
                resolver,
                IdGeneratorFactory.CreateSnowflakeIdGenerator_LowWorkload(),
                tenant);

            var loginTime = DateTimeOffset.UtcNow.AddMinutes(-1);
            var before = DateTimeOffset.UtcNow;
            await writer.WriteAsync(new LoginLogRecord
            {
                TenantId = RecordTenantId,
                TraceId = "trace-login",
                UserName = "tester",
                LoginResult = 1,
                LoginIp = "10.0.0.1",
                LoginTime = loginTime
            });
            var after = DateTimeOffset.UtcNow;

            AssertRouted(resolver, typeof(SysLoginLog));
            AssertWrittenInRecordTenant(resolver, tenant);

            var range = CurrentUtcMonthRange();
            var found = db.Queryable<SysLoginLog>()
                .SplitTable(range[0], range[1])
                .Where(item => item.TraceId == "trace-login")
                .ToList();

            var row = Assert.Single(found);
            Assert.Equal(RecordTenantId, row.TenantId);
            Assert.NotEqual(0L, row.BasicId);
            Assert.Equal("tester", row.UserName);
            Assert.Equal(1, row.LoginResult);
            AssertCreatedTimeNearNow(row.CreatedTime, before, after);
        }
        finally
        {
            DeleteDatabase(databaseFile);
        }
    }

    [Fact]
    public async Task 操作日志写入器按实体类型路由并落入当月分表()
    {
        var databaseFile = NewDatabasePath();

        try
        {
            using var db = CreateClient(databaseFile);

            db.CodeFirst.SplitTables().InitTables(typeof(SysOperationLog));

            var tenant = new RecordingCurrentTenant(AmbientTenantId);
            var resolver = new StubClientResolver(db, () => tenant.Id);
            var writer = new SqlSugarOperationLogWriter(
                resolver,
                IdGeneratorFactory.CreateSnowflakeIdGenerator_LowWorkload(),
                tenant);

            var before = DateTimeOffset.UtcNow;
            await writer.WriteAsync(new OperationLogRecord
            {
                TenantId = RecordTenantId,
                TraceId = "trace-operation",
                Method = "POST",
                Path = "/Order",
                StatusCode = 201,
                ElapsedMilliseconds = 5
            });
            var after = DateTimeOffset.UtcNow;

            AssertRouted(resolver, typeof(SysOperationLog));
            AssertWrittenInRecordTenant(resolver, tenant);

            var range = CurrentUtcMonthRange();
            var found = db.Queryable<SysOperationLog>()
                .SplitTable(range[0], range[1])
                .Where(item => item.TraceId == "trace-operation")
                .ToList();

            var row = Assert.Single(found);
            Assert.Equal(RecordTenantId, row.TenantId);
            Assert.NotEqual(0L, row.BasicId);
            Assert.Equal(201, row.StatusCode);
            AssertCreatedTimeNearNow(row.CreatedTime, before, after);
        }
        finally
        {
            DeleteDatabase(databaseFile);
        }
    }

    [Fact]
    public async Task 实体差异日志写入器经当前工作单元客户端写入并落入当月分表()
    {
        var databaseFile = NewDatabasePath();

        try
        {
            using var db = CreateClient(databaseFile);

            db.CodeFirst.SplitTables().InitTables(typeof(SysDiffLog));

            var resolver = new StubClientResolver(db);
            var writer = new SqlSugarEntityDiffLogWriter(
                resolver,
                IdGeneratorFactory.CreateSnowflakeIdGenerator_LowWorkload());

            var before = DateTimeOffset.UtcNow;
            await writer.WriteAsync(new EntityDiffLogRecord
            {
                OperationType = "Update",
                EntityType = "Order",
                EntityId = "1001",
                ChangedFields = "[{\"Field\":\"Status\"}]"
            });
            var after = DateTimeOffset.UtcNow;

            AssertUsedCurrentClient(resolver);

            var range = CurrentUtcMonthRange();
            var found = db.Queryable<SysDiffLog>()
                .SplitTable(range[0], range[1])
                .Where(item => item.EntityId == "1001")
                .ToList();

            var row = Assert.Single(found);
            Assert.NotEqual(0L, row.BasicId);
            Assert.Equal("Update", row.OperationType);
            Assert.Equal("EntityChange", row.AuditType);
            AssertCreatedTimeNearNow(row.CreatedTime, before, after);
        }
        finally
        {
            DeleteDatabase(databaseFile);
        }
    }

    [Fact]
    public async Task 平台记录取客户端时租户为平台并在写入后还原环境租户()
    {
        var databaseFile = NewDatabasePath();

        try
        {
            using var db = CreateClient(databaseFile);

            db.CodeFirst.SplitTables().InitTables(typeof(SysAccessLog));

            var tenant = new RecordingCurrentTenant(AmbientTenantId);
            var resolver = new StubClientResolver(db, () => tenant.Id);
            var writer = new SqlSugarAccessLogWriter(
                resolver,
                IdGeneratorFactory.CreateSnowflakeIdGenerator_LowWorkload(),
                tenant);

            await writer.WriteAsync(new AccessLogRecord { TraceId = "trace-platform", Method = "GET", Path = "/" });

            Assert.Null(Assert.Single(resolver.TenantIdsAtResolve));
            Assert.Equal(AmbientTenantId, tenant.Id);
        }
        finally
        {
            DeleteDatabase(databaseFile);
        }
    }

    [Fact]
    public async Task 挂真实数据执行处理器时平台记录落0且租户记录落记录租户()
    {
        var databaseFile = NewDatabasePath();
        AsyncLocalCurrentTenantAccessor.Instance.Current = null;

        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<ICurrentTenantAccessor>(AsyncLocalCurrentTenantAccessor.Instance);
            services.AddTransient<ICurrentTenant, CurrentTenant>();
            using var provider = services.BuildServiceProvider();
            var tenant = provider.GetRequiredService<ICurrentTenant>();
            var idGenerator = IdGeneratorFactory.CreateSnowflakeIdGenerator_LowWorkload();
            var handler = new SqlSugarDataExecutingHandler(
                provider.GetRequiredService<IServiceScopeFactory>(),
                idGenerator);

            using var db = CreateClient(databaseFile);
            db.Aop.DataExecuting = (_, entityInfo) => handler.Handle(entityInfo);
            db.CodeFirst.SplitTables().InitTables(typeof(SysAccessLog));

            var writer = new SqlSugarAccessLogWriter(new StubClientResolver(db), idGenerator, tenant);

            using (tenant.Change(AmbientTenantId))
            {
                await writer.WriteAsync(new AccessLogRecord { TraceId = "trace-real-platform", Method = "GET", Path = "/" });
                await writer.WriteAsync(new AccessLogRecord { TenantId = RecordTenantId, TraceId = "trace-real-tenant", Method = "GET", Path = "/" });
                Assert.Equal(AmbientTenantId, tenant.Id);
            }

            var range = CurrentUtcMonthRange();
            var rows = db.Queryable<SysAccessLog>()
                .SplitTable(range[0], range[1])
                .Where(item => item.TraceId.StartsWith("trace-real-"))
                .ToList();

            Assert.Equal(0L, Assert.Single(rows, item => item.TraceId == "trace-real-platform").TenantId);
            Assert.Equal(RecordTenantId, Assert.Single(rows, item => item.TraceId == "trace-real-tenant").TenantId);
        }
        finally
        {
            AsyncLocalCurrentTenantAccessor.Instance.Current = null;
            DeleteDatabase(databaseFile);
        }
    }

    /// <summary>
    /// 注册扩展以 SqlSugar 写入器顶替空写入器
    /// </summary>
    [Fact]
    public void 注册扩展顶替空写入器()
    {
        var services = new ServiceCollection();
        services.TryAddScoped<IOperationLogWriter, NullOperationLogWriter>();

        services.AddXiHanAuditingSqlSugar();

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IOperationLogWriter));
        Assert.Equal(typeof(SqlSugarOperationLogWriter), descriptor.ImplementationType);
    }

    /// <summary>
    /// 六个写入器全部被注册扩展顶替
    /// </summary>
    [Theory]
    [InlineData(typeof(IAccessLogWriter), typeof(SqlSugarAccessLogWriter))]
    [InlineData(typeof(IApiLogWriter), typeof(SqlSugarApiLogWriter))]
    [InlineData(typeof(IEntityDiffLogWriter), typeof(SqlSugarEntityDiffLogWriter))]
    [InlineData(typeof(IExceptionLogWriter), typeof(SqlSugarExceptionLogWriter))]
    [InlineData(typeof(ILoginLogWriter), typeof(SqlSugarLoginLogWriter))]
    [InlineData(typeof(IOperationLogWriter), typeof(SqlSugarOperationLogWriter))]
    public void 六个写入器全部被顶替(Type serviceType, Type expectedImplementationType)
    {
        var services = new ServiceCollection();
        services.TryAddScoped<IAccessLogWriter, NullAccessLogWriter>();
        services.TryAddScoped<IApiLogWriter, NullApiLogWriter>();
        services.TryAddScoped<IEntityDiffLogWriter, NullEntityDiffLogWriter>();
        services.TryAddScoped<IExceptionLogWriter, NullExceptionLogWriter>();
        services.TryAddScoped<ILoginLogWriter, NullLoginLogWriter>();
        services.TryAddScoped<IOperationLogWriter, NullOperationLogWriter>();

        services.AddXiHanAuditingSqlSugar();

        var descriptor = Assert.Single(services, item => item.ServiceType == serviceType);
        Assert.Equal(expectedImplementationType, descriptor.ImplementationType);
    }

    private static DateTime[] CurrentUtcMonthRange()
    {
        var now = DateTimeOffset.UtcNow;
        var begin = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return [begin, begin.AddMonths(1).AddTicks(-1)];
    }

    private static void AssertRouted(StubClientResolver resolver, Type expectedEntityType)
    {
        Assert.Equal(expectedEntityType, Assert.Single(resolver.RequestedEntityTypes));
        Assert.Equal(0, resolver.GetCurrentClientCalls);
        Assert.Equal(0, resolver.GetClientCalls);
    }

    private static void AssertWrittenInRecordTenant(StubClientResolver resolver, RecordingCurrentTenant tenant)
    {
        Assert.Equal(RecordTenantId, Assert.Single(resolver.TenantIdsAtResolve));
        Assert.Equal(AmbientTenantId, tenant.Id);
    }

    private static void AssertUsedCurrentClient(StubClientResolver resolver)
    {
        Assert.Empty(resolver.RequestedEntityTypes);
        Assert.Equal(1, resolver.GetCurrentClientCalls);
        Assert.Equal(0, resolver.GetClientCalls);
    }

    private static void AssertCreatedTimeNearNow(DateTimeOffset actual, DateTimeOffset before, DateTimeOffset after)
    {
        // SQLite 的 DateTimeOffset 列不保存偏移，回读按本地时区解析，因此比对写入的时钟读数而非瞬时
        Assert.InRange(
            actual.DateTime,
            before.UtcDateTime.AddSeconds(-5),
            after.UtcDateTime.AddSeconds(5));
    }

    private static string NewDatabasePath()
    {
        return Path.Combine(Path.GetTempPath(), $"xihan_writer_{Guid.NewGuid():N}.db");
    }

    private static SqlSugarClient CreateClient(string databaseFile)
    {
        // 关闭连接池，避免用例结束后驱动仍持有临时库文件句柄。
        return new(new ConnectionConfig
        {
            ConnectionString = $"DataSource={databaseFile};Pooling=False",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true
        });
    }

    private static void DeleteDatabase(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

/// <summary>
/// 测试用客户端解析器：固定返回同一个客户端，并记录被请求的是哪个实体类型
/// </summary>
internal sealed class StubClientResolver : ISqlSugarClientResolver
{
    private readonly ISqlSugarClient _client;
    private readonly Func<long?>? _tenantProbe;

    public StubClientResolver(ISqlSugarClient client, Func<long?>? tenantProbe = null)
    {
        _client = client;
        _tenantProbe = tenantProbe;
    }

    public List<long?> TenantIdsAtResolve { get; } = [];

    public List<Type> RequestedEntityTypes { get; } = [];

    public int GetCurrentClientCalls { get; private set; }

    public int GetClientCalls { get; private set; }

    public ISqlSugarClient GetCurrentClient()
    {
        GetCurrentClientCalls++;
        return _client;
    }

    public ISqlSugarClient GetClientForEntity(Type entityType)
    {
        RequestedEntityTypes.Add(entityType);
        TenantIdsAtResolve.Add(_tenantProbe?.Invoke());
        return _client;
    }

    public ISqlSugarClient GetClient(string configId)
    {
        GetClientCalls++;
        return _client;
    }

    public IReadOnlyCollection<string> GetAllConfigIds()
    {
        return ["Default"];
    }

    public IReadOnlyList<string> GetCurrentLayoutConfigIds()
    {
        return ["Default"];
    }

    public IEnumerable<ISqlSugarClient> GetAllClients()
    {
        return [_client];
    }

    public ITenant AsTenant()
    {
        throw new NotSupportedException("测试桩不支持多租户切换。");
    }
}

/// <summary>
/// 测试用当前租户：Change 期间替换标识，释放后还原
/// </summary>
internal sealed class RecordingCurrentTenant : ICurrentTenant
{
    public RecordingCurrentTenant(long? id)
    {
        Id = id;
    }

    public bool IsAvailable => Id is > 0;

    public long? Id { get; private set; }

    public string? Name => null;

    public IDisposable Change(long? id, string? name = null)
    {
        var previous = Id;
        Id = id;
        return new Restore(this, previous);
    }

    private sealed class Restore(RecordingCurrentTenant owner, long? previous) : IDisposable
    {
        public void Dispose()
        {
            owner.Id = previous;
        }
    }
}
