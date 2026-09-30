// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs;
using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Extensions.DependencyInjection;
using XiHan.Framework.Tasks.BackgroundJobs.Management;
using XiHan.Framework.Tasks.BackgroundJobs.Models;
using XiHan.Framework.Tasks.BackgroundJobs.Options;
using XiHan.Framework.Tasks.Tests.BackgroundJobs.Fakes;
using XiHan.Framework.Timing;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs.Management;

/// <summary>
/// 后台作业管理服务测试
/// </summary>
public class BackgroundJobManagementServiceTests
{
    private const int TimeoutMilliseconds = 10000;

    private static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// 默认授权器拒绝：返回 Denied，存储无变更，审计收到 Denied
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task RequestCancellation_WithDenyAllAuthorizer_ReturnsDenied_AndLeavesStoreUnchanged()
    {
        var clock = new FakeClock(Now);
        var store = CreateStore(clock);
        var job = CreateJob();
        await store.InsertAsync(job);
        var auditor = new RecordingAuditor();
        var service = CreateService(store, new DenyAllBackgroundJobManagementAuthorizer(), auditor, clock);

        var result = await service.RequestCancellationAsync(job.Id, TestContext.Current.CancellationToken);

        Assert.Equal(new BackgroundJobManagementResult(job.Id, BackgroundJobManagementOperation.Cancel, BackgroundJobManagementStatus.Denied), result);
        Assert.False(result.IsChanged);
        Assert.Same(job, await store.FindAsync(job.Id));
        Assert.False(job.IsCancellationRequested);
        var entry = Assert.Single(auditor.Entries);
        Assert.Equal(new BackgroundJobManagementAuditEntry(job.Id, BackgroundJobManagementOperation.Cancel, BackgroundJobManagementStatus.Denied, Now), entry);
    }

    /// <summary>
    /// 默认授权器拒绝重试：返回 Denied，已放弃的作业保持放弃
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Retry_WithDenyAllAuthorizer_ReturnsDenied_AndLeavesStoreUnchanged()
    {
        var clock = new FakeClock(Now);
        var store = CreateStore(clock);
        var job = CreateJob();
        await store.InsertAsync(job);
        job.IsAbandoned = true;
        var auditor = new RecordingAuditor();
        var service = CreateService(store, new DenyAllBackgroundJobManagementAuthorizer(), auditor, clock);

        var result = await service.RetryAsync(job.Id, TestContext.Current.CancellationToken);

        Assert.Equal(BackgroundJobManagementStatus.Denied, result.Status);
        Assert.Equal(BackgroundJobManagementOperation.Retry, result.Operation);
        Assert.True(job.IsAbandoned);
        Assert.Equal(BackgroundJobManagementStatus.Denied, Assert.Single(auditor.Entries).Status);
    }

    /// <summary>
    /// 授权通过时重试委派给存储并审计 Rescheduled
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Retry_WhenAuthorized_DelegatesToStore_AndAudits()
    {
        var clock = new FakeClock(Now);
        var store = CreateStore(clock);
        var job = CreateJob();
        job.TryCount = 4;
        await store.InsertAsync(job);
        job.IsAbandoned = true;
        var auditor = new RecordingAuditor();
        var authorizer = new StubAuthorizer(true);
        var service = CreateService(store, authorizer, auditor, clock);

        var result = await service.RetryAsync(job.Id, TestContext.Current.CancellationToken);

        Assert.Equal(new BackgroundJobManagementResult(job.Id, BackgroundJobManagementOperation.Retry, BackgroundJobManagementStatus.Rescheduled), result);
        Assert.True(result.IsChanged);
        Assert.False(job.IsAbandoned);
        Assert.Equal((short)0, job.TryCount);
        Assert.Equal((BackgroundJobManagementOperation.Retry, job.Id), Assert.Single(authorizer.Calls));
        Assert.Equal(new BackgroundJobManagementAuditEntry(job.Id, BackgroundJobManagementOperation.Retry, BackgroundJobManagementStatus.Rescheduled, Now), Assert.Single(auditor.Entries));
    }

    /// <summary>
    /// 授权通过时取消等待中的作业委派给存储并审计 Cancelled
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task RequestCancellation_WhenAuthorized_DelegatesToStore_AndAudits()
    {
        var clock = new FakeClock(Now);
        var store = CreateStore(clock);
        var job = CreateJob();
        await store.InsertAsync(job);
        var auditor = new RecordingAuditor();
        var authorizer = new StubAuthorizer(true);
        var service = CreateService(store, authorizer, auditor, clock);

        var result = await service.RequestCancellationAsync(job.Id, TestContext.Current.CancellationToken);

        Assert.Equal(BackgroundJobManagementStatus.Cancelled, result.Status);
        Assert.Null(await store.FindAsync(job.Id));
        Assert.Equal((BackgroundJobManagementOperation.Cancel, job.Id), Assert.Single(authorizer.Calls));
        Assert.Equal(BackgroundJobManagementStatus.Cancelled, Assert.Single(auditor.Entries).Status);
    }

    /// <summary>
    /// 作业不存在时返回 NotFound 并审计
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Retry_WhenJobMissing_ReturnsNotFound_AndAudits()
    {
        var clock = new FakeClock(Now);
        var auditor = new RecordingAuditor();
        var service = CreateService(CreateStore(clock), new StubAuthorizer(true), auditor, clock);
        var jobId = Guid.NewGuid();

        var result = await service.RetryAsync(jobId, TestContext.Current.CancellationToken);

        Assert.Equal(BackgroundJobManagementStatus.NotFound, result.Status);
        Assert.Equal(BackgroundJobManagementStatus.NotFound, Assert.Single(auditor.Entries).Status);
    }

    /// <summary>
    /// 存储不支持作业管理时返回 NotSupported 并审计，且不调用存储的管理方法
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Operations_WhenStoreDoesNotSupportManagement_ReturnNotSupported_AndAudit()
    {
        var clock = new FakeClock(Now);
        var auditor = new RecordingAuditor();
        var service = CreateService(new RecordingBackgroundJobStore(), new StubAuthorizer(true), auditor, clock);
        var jobId = Guid.NewGuid();

        var retry = await service.RetryAsync(jobId, TestContext.Current.CancellationToken);
        var cancel = await service.RequestCancellationAsync(jobId, TestContext.Current.CancellationToken);

        Assert.Equal(BackgroundJobManagementStatus.NotSupported, retry.Status);
        Assert.Equal(BackgroundJobManagementStatus.NotSupported, cancel.Status);
        Assert.Equal(
            [BackgroundJobManagementStatus.NotSupported, BackgroundJobManagementStatus.NotSupported],
            auditor.Entries.Select(x => x.Status));
    }

    /// <summary>
    /// 执行中的作业两次取消：第一次登记取消请求，第二次无变更
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task RequestCancellation_Twice_OnLeasedJob_ReturnsRequestedThenNoChange()
    {
        var clock = new FakeClock(Now);
        var store = CreateStore(clock);
        var job = CreateJob();
        await store.InsertAsync(job);
        Assert.Single(await store.GetWaitingJobsAsync(null, 10));
        var auditor = new RecordingAuditor();
        var service = CreateService(store, new StubAuthorizer(true), auditor, clock);

        var first = await service.RequestCancellationAsync(job.Id, TestContext.Current.CancellationToken);
        var second = await service.RequestCancellationAsync(job.Id, TestContext.Current.CancellationToken);

        Assert.Equal(BackgroundJobManagementStatus.CancellationRequested, first.Status);
        Assert.Equal(BackgroundJobManagementStatus.NoChange, second.Status);
        Assert.False(second.IsChanged);
        Assert.True(job.IsCancellationRequested);
        Assert.Equal(
            [BackgroundJobManagementStatus.CancellationRequested, BackgroundJobManagementStatus.NoChange],
            auditor.Entries.Select(x => x.Status));
    }

    /// <summary>
    /// 并发 20 次取消同一执行中的作业：恰好一次登记取消请求，其余无变更
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task RequestCancellation_Concurrent_OnLeasedJob_RequestsExactlyOnce()
    {
        const int Concurrency = 20;
        var clock = new FakeClock(Now);
        var store = CreateStore(clock);
        var job = CreateJob();
        await store.InsertAsync(job);
        Assert.Single(await store.GetWaitingJobsAsync(null, 10));
        var auditor = new RecordingAuditor();
        var service = CreateService(store, new StubAuthorizer(true), auditor, clock);
        var cancellationToken = TestContext.Current.CancellationToken;

        var results = await Task.WhenAll(Enumerable.Range(0, Concurrency)
            .Select(_ => Task.Run(() => service.RequestCancellationAsync(job.Id, cancellationToken), cancellationToken)));

        Assert.Single(results, x => x.Status == BackgroundJobManagementStatus.CancellationRequested);
        Assert.Equal(Concurrency - 1, results.Count(x => x.Status == BackgroundJobManagementStatus.NoChange));
        Assert.Equal(Concurrency, auditor.Entries.Count);
    }

    /// <summary>
    /// 审计器抛异常时结果不变，并记录一条 Warning
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task RequestCancellation_WhenAuditorThrows_KeepsResult_AndLogsWarning()
    {
        var clock = new FakeClock(Now);
        var store = CreateStore(clock);
        var job = CreateJob();
        await store.InsertAsync(job);
        var logger = new RecordingLogger<BackgroundJobManagementService>();
        var service = new BackgroundJobManagementService(store, new StubAuthorizer(true), new ThrowingAuditor(), clock, logger);

        var result = await service.RequestCancellationAsync(job.Id, TestContext.Current.CancellationToken);

        Assert.Equal(BackgroundJobManagementStatus.Cancelled, result.Status);
        Assert.Null(await store.FindAsync(job.Id));
        Assert.Contains(LogLevel.Warning, logger.Entries);
    }

    /// <summary>
    /// 授权器抛异常时异常向外传播，存储不变更且不审计
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task RequestCancellation_WhenAuthorizerThrows_Propagates_AndDoesNotAudit()
    {
        var clock = new FakeClock(Now);
        var store = CreateStore(clock);
        var job = CreateJob();
        await store.InsertAsync(job);
        var auditor = new RecordingAuditor();
        var service = CreateService(store, new ThrowingAuthorizer(), auditor, clock);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RequestCancellationAsync(job.Id, TestContext.Current.CancellationToken));

        Assert.Same(job, await store.FindAsync(job.Id));
        Assert.Empty(auditor.Entries);
    }

    /// <summary>
    /// 授权器收到的操作与作业标识与调用一致
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Operations_PassOperationAndJobIdToAuthorizer()
    {
        var clock = new FakeClock(Now);
        var authorizer = new StubAuthorizer(false);
        var service = CreateService(CreateStore(clock), authorizer, new RecordingAuditor(), clock);
        var retryJobId = Guid.NewGuid();
        var cancelJobId = Guid.NewGuid();

        await service.RetryAsync(retryJobId, TestContext.Current.CancellationToken);
        await service.RequestCancellationAsync(cancelJobId, TestContext.Current.CancellationToken);

        Assert.Equal(
            [(BackgroundJobManagementOperation.Retry, retryJobId), (BackgroundJobManagementOperation.Cancel, cancelJobId)],
            authorizer.Calls);
    }

    /// <summary>
    /// 调用方令牌在存储操作之后被取消：结果照常返回，审计器仍收到记录
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task RequestCancellation_WhenCallerTokenCancelledAfterStoreOperation_StillAudits()
    {
        var clock = new FakeClock(Now);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var store = new ScriptedManagementStore
        {
            OnRequestCancellation = _ =>
            {
                cts.Cancel();
                return Task.FromResult(BackgroundJobManagementStatus.CancellationRequested);
            }
        };
        var auditor = new TokenHonoringAuditor();
        var service = CreateService(store, new StubAuthorizer(true), auditor, clock);
        var jobId = Guid.NewGuid();

        var result = await service.RequestCancellationAsync(jobId, cts.Token);

        Assert.Equal(BackgroundJobManagementStatus.CancellationRequested, result.Status);
        var entry = Assert.Single(auditor.Entries);
        Assert.Equal(new BackgroundJobManagementAuditEntry(jobId, BackgroundJobManagementOperation.Cancel, BackgroundJobManagementStatus.CancellationRequested, Now), entry);
    }

    /// <summary>
    /// 存储抛异常时异常向外传播且不审计
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Retry_WhenStoreThrows_Propagates_AndDoesNotAudit()
    {
        var clock = new FakeClock(Now);
        var store = new ScriptedManagementStore
        {
            OnRetry = _ => Task.FromException<BackgroundJobManagementStatus>(new InvalidOperationException("模拟存储失败"))
        };
        var auditor = new RecordingAuditor();
        var service = CreateService(store, new StubAuthorizer(true), auditor, clock);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RetryAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));

        Assert.Empty(auditor.Entries);
    }

    /// <summary>
    /// 调用方令牌一开始即已取消：抛出取消异常，不授权、不变更存储、不审计
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task RequestCancellation_WhenCallerTokenAlreadyCancelled_Throws_AndLeavesStoreUnchanged()
    {
        var clock = new FakeClock(Now);
        var store = CreateStore(clock);
        var job = CreateJob();
        await store.InsertAsync(job);
        var authorizer = new StubAuthorizer(true);
        var auditor = new RecordingAuditor();
        var service = CreateService(store, authorizer, auditor, clock);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.RequestCancellationAsync(job.Id, new CancellationToken(true)));

        Assert.Same(job, await store.FindAsync(job.Id));
        Assert.False(job.IsCancellationRequested);
        Assert.Empty(authorizer.Calls);
        Assert.Empty(auditor.Entries);
    }

    /// <summary>
    /// 默认授权器对任意操作都拒绝
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task DenyAllAuthorizer_AlwaysReturnsFalse()
    {
        var authorizer = new DenyAllBackgroundJobManagementAuthorizer();

        Assert.False(await authorizer.IsAuthorizedAsync(BackgroundJobManagementOperation.Retry, Guid.NewGuid(), TestContext.Current.CancellationToken));
        Assert.False(await authorizer.IsAuthorizedAsync(BackgroundJobManagementOperation.Cancel, Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 默认审计器以 Information 级别记录
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task LoggingAuditor_LogsInformation()
    {
        var logger = new RecordingLogger<LoggingBackgroundJobManagementAuditor>();
        var auditor = new LoggingBackgroundJobManagementAuditor(logger);

        await auditor.AuditAsync(
            new BackgroundJobManagementAuditEntry(Guid.NewGuid(), BackgroundJobManagementOperation.Cancel, BackgroundJobManagementStatus.Denied, Now),
            TestContext.Current.CancellationToken);

        Assert.Equal([LogLevel.Information], logger.Entries);
    }

    /// <summary>
    /// 装配登记管理服务、默认授权器与默认审计器，生命周期符合约定且可解析
    /// </summary>
    [Fact]
    public void AddXiHanBackgroundJobs_RegistersManagementServices()
    {
        var services = CreateServices();

        services.AddXiHanBackgroundJobs(CreateConfiguration());

        AssertSingleDescriptor(services, typeof(IBackgroundJobManagementService), typeof(BackgroundJobManagementService), ServiceLifetime.Transient);
        AssertSingleDescriptor(services, typeof(IBackgroundJobManagementAuthorizer), typeof(DenyAllBackgroundJobManagementAuthorizer), ServiceLifetime.Singleton);
        AssertSingleDescriptor(services, typeof(IBackgroundJobManagementAuditor), typeof(LoggingBackgroundJobManagementAuditor), ServiceLifetime.Singleton);

        using var provider = services.BuildServiceProvider();
        Assert.IsType<BackgroundJobManagementService>(provider.GetRequiredService<IBackgroundJobManagementService>());
        Assert.IsType<DenyAllBackgroundJobManagementAuthorizer>(provider.GetRequiredService<IBackgroundJobManagementAuthorizer>());
        Assert.IsType<LoggingBackgroundJobManagementAuditor>(provider.GetRequiredService<IBackgroundJobManagementAuditor>());
    }

    /// <summary>
    /// 应用侧先注册的授权器、审计器与管理服务不会被框架默认实现顶掉
    /// </summary>
    [Fact]
    public void AddXiHanBackgroundJobs_DoesNotOverrideApplicationProvidedManagementServices()
    {
        var services = CreateServices();
        var authorizer = new StubAuthorizer(true);
        var auditor = new RecordingAuditor();
        var managementService = new StubManagementService();
        services.AddSingleton<IBackgroundJobManagementAuthorizer>(authorizer);
        services.AddSingleton<IBackgroundJobManagementAuditor>(auditor);
        services.AddSingleton<IBackgroundJobManagementService>(managementService);

        services.AddXiHanBackgroundJobs(CreateConfiguration());

        using var provider = services.BuildServiceProvider();
        Assert.Same(authorizer, provider.GetRequiredService<IBackgroundJobManagementAuthorizer>());
        Assert.Same(auditor, provider.GetRequiredService<IBackgroundJobManagementAuditor>());
        Assert.Same(managementService, provider.GetRequiredService<IBackgroundJobManagementService>());
    }

    /// <summary>
    /// 替换授权器后，从容器解析的管理服务使用替换的授权器
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task AddXiHanBackgroundJobs_ResolvedServiceUsesReplacedAuthorizerAndAuditor()
    {
        var services = CreateServices();
        var auditor = new RecordingAuditor();
        services.AddSingleton<IBackgroundJobManagementAuthorizer>(new StubAuthorizer(true));
        services.AddSingleton<IBackgroundJobManagementAuditor>(auditor);
        services.AddXiHanBackgroundJobs(CreateConfiguration());
        using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IBackgroundJobStore>();
        var job = CreateJob();
        await store.InsertAsync(job);

        var result = await provider.GetRequiredService<IBackgroundJobManagementService>()
            .RequestCancellationAsync(job.Id, TestContext.Current.CancellationToken);

        Assert.Equal(BackgroundJobManagementStatus.Cancelled, result.Status);
        Assert.Equal(BackgroundJobManagementStatus.Cancelled, Assert.Single(auditor.Entries).Status);
    }

    /// <summary>
    /// 创建进程内存储
    /// </summary>
    private static DefaultBackgroundJobStore CreateStore(FakeClock clock)
    {
        return new DefaultBackgroundJobStore(
            clock,
            Microsoft.Extensions.Options.Options.Create(new BackgroundJobWorkerOptions { JobLeaseDurationSeconds = 60 }));
    }

    /// <summary>
    /// 创建管理服务
    /// </summary>
    private static BackgroundJobManagementService CreateService(
        IBackgroundJobStore store,
        IBackgroundJobManagementAuthorizer authorizer,
        IBackgroundJobManagementAuditor auditor,
        IClock clock)
    {
        return new BackgroundJobManagementService(store, authorizer, auditor, clock, NullLogger<BackgroundJobManagementService>.Instance);
    }

    /// <summary>
    /// 构造一条可立即执行的作业记录
    /// </summary>
    private static BackgroundJobInfo CreateJob()
    {
        return new BackgroundJobInfo
        {
            Id = Guid.NewGuid(),
            JobName = "job",
            JobArgs = "{}",
            CreationTime = Now,
            NextTryTime = Now
        };
    }

    /// <summary>
    /// 创建带最小依赖的服务集合
    /// </summary>
    private static IServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClock>(new FakeClock(Now));
        services.AddSingleton<ICurrentTenant>(new FakeCurrentTenant());
        return services;
    }

    /// <summary>
    /// 创建空配置
    /// </summary>
    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder().Build();
    }

    /// <summary>
    /// 断言某个服务类型只登记了一条描述符，且实现类型与生命周期符合预期
    /// </summary>
    private static void AssertSingleDescriptor(IServiceCollection services, Type serviceType, Type implementationType, ServiceLifetime lifetime)
    {
        var descriptor = Assert.Single(services, x => x.ServiceType == serviceType);

        Assert.Equal(implementationType, descriptor.ImplementationType);
        Assert.Equal(lifetime, descriptor.Lifetime);
    }

    /// <summary>
    /// 返回固定结论并记录调用的授权器
    /// </summary>
    private sealed class StubAuthorizer(bool isAuthorized) : IBackgroundJobManagementAuthorizer
    {
        private readonly Lock _gate = new();
        private readonly List<(BackgroundJobManagementOperation, Guid)> _calls = [];

        /// <summary>
        /// 已收到的授权请求
        /// </summary>
        public IReadOnlyList<(BackgroundJobManagementOperation, Guid)> Calls
        {
            get
            {
                lock (_gate)
                {
                    return [.. _calls];
                }
            }
        }

        /// <summary>
        /// 判断是否授权
        /// </summary>
        public Task<bool> IsAuthorizedAsync(BackgroundJobManagementOperation operation, Guid jobId, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                _calls.Add((operation, jobId));
            }

            return Task.FromResult(isAuthorized);
        }
    }

    /// <summary>
    /// 总是抛异常的授权器
    /// </summary>
    private sealed class ThrowingAuthorizer : IBackgroundJobManagementAuthorizer
    {
        /// <summary>
        /// 判断是否授权
        /// </summary>
        public Task<bool> IsAuthorizedAsync(BackgroundJobManagementOperation operation, Guid jobId, CancellationToken cancellationToken = default)
        {
            return Task.FromException<bool>(new InvalidOperationException("模拟授权失败"));
        }
    }

    /// <summary>
    /// 记录审计条目的审计器
    /// </summary>
    private sealed class RecordingAuditor : IBackgroundJobManagementAuditor
    {
        private readonly Lock _gate = new();
        private readonly List<BackgroundJobManagementAuditEntry> _entries = [];

        /// <summary>
        /// 已记录的审计条目
        /// </summary>
        public IReadOnlyList<BackgroundJobManagementAuditEntry> Entries
        {
            get
            {
                lock (_gate)
                {
                    return [.. _entries];
                }
            }
        }

        /// <summary>
        /// 记录审计条目
        /// </summary>
        public Task AuditAsync(BackgroundJobManagementAuditEntry entry, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                _entries.Add(entry);
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 按取消令牌行事的审计器：令牌已取消时抛出，否则记录
    /// </summary>
    private sealed class TokenHonoringAuditor : IBackgroundJobManagementAuditor
    {
        private readonly Lock _gate = new();
        private readonly List<BackgroundJobManagementAuditEntry> _entries = [];

        /// <summary>
        /// 已记录的审计条目
        /// </summary>
        public IReadOnlyList<BackgroundJobManagementAuditEntry> Entries
        {
            get
            {
                lock (_gate)
                {
                    return [.. _entries];
                }
            }
        }

        /// <summary>
        /// 记录审计条目
        /// </summary>
        public Task AuditAsync(BackgroundJobManagementAuditEntry entry, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                _entries.Add(entry);
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 管理方法行为可编排的存储替身
    /// </summary>
    private sealed class ScriptedManagementStore : IBackgroundJobStore
    {
        /// <summary>
        /// 重试时的行为
        /// </summary>
        public Func<Guid, Task<BackgroundJobManagementStatus>> OnRetry { get; init; } = _ => Task.FromResult(BackgroundJobManagementStatus.NoChange);

        /// <summary>
        /// 请求取消时的行为
        /// </summary>
        public Func<Guid, Task<BackgroundJobManagementStatus>> OnRequestCancellation { get; init; } = _ => Task.FromResult(BackgroundJobManagementStatus.NoChange);

        /// <summary>
        /// 支持作业管理
        /// </summary>
        public bool SupportsJobManagement => true;

        /// <summary>
        /// 按标识查找作业
        /// </summary>
        public Task<BackgroundJobInfo?> FindAsync(Guid jobId)
        {
            return Task.FromResult<BackgroundJobInfo?>(null);
        }

        /// <summary>
        /// 插入作业
        /// </summary>
        public Task InsertAsync(BackgroundJobInfo jobInfo)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 获取待执行作业
        /// </summary>
        public Task<List<BackgroundJobInfo>> GetWaitingJobsAsync(string? applicationName, int maxResultCount)
        {
            return Task.FromResult(new List<BackgroundJobInfo>());
        }

        /// <summary>
        /// 删除作业
        /// </summary>
        public Task DeleteAsync(Guid jobId)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 更新作业
        /// </summary>
        public Task UpdateAsync(BackgroundJobInfo jobInfo)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 重试已放弃的作业
        /// </summary>
        public Task<BackgroundJobManagementStatus> RetryAbandonedAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            return OnRetry(jobId);
        }

        /// <summary>
        /// 请求取消作业
        /// </summary>
        public Task<BackgroundJobManagementStatus> RequestCancellationAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            return OnRequestCancellation(jobId);
        }
    }

    /// <summary>
    /// 总是抛异常的审计器
    /// </summary>
    private sealed class ThrowingAuditor : IBackgroundJobManagementAuditor
    {
        /// <summary>
        /// 记录审计条目
        /// </summary>
        public Task AuditAsync(BackgroundJobManagementAuditEntry entry, CancellationToken cancellationToken = default)
        {
            return Task.FromException(new InvalidOperationException("模拟审计失败"));
        }
    }

    /// <summary>
    /// 应用侧自定义管理服务替身
    /// </summary>
    private sealed class StubManagementService : IBackgroundJobManagementService
    {
        /// <summary>
        /// 重试已放弃的作业
        /// </summary>
        public Task<BackgroundJobManagementResult> RetryAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new BackgroundJobManagementResult(jobId, BackgroundJobManagementOperation.Retry, BackgroundJobManagementStatus.NoChange));
        }

        /// <summary>
        /// 请求取消作业
        /// </summary>
        public Task<BackgroundJobManagementResult> RequestCancellationAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new BackgroundJobManagementResult(jobId, BackgroundJobManagementOperation.Cancel, BackgroundJobManagementStatus.NoChange));
        }
    }

    /// <summary>
    /// 只记录日志级别的日志器
    /// </summary>
    private sealed class RecordingLogger<TCategory> : ILogger<TCategory>
    {
        private readonly Lock _gate = new();
        private readonly List<LogLevel> _entries = [];

        /// <summary>
        /// 已记录的日志级别
        /// </summary>
        public IReadOnlyList<LogLevel> Entries
        {
            get
            {
                lock (_gate)
                {
                    return [.. _entries];
                }
            }
        }

        /// <summary>
        /// 开始日志作用域
        /// </summary>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        /// <summary>
        /// 是否启用指定级别
        /// </summary>
        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        /// <summary>
        /// 写日志
        /// </summary>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (_gate)
            {
                _entries.Add(logLevel);
            }
        }
    }
}
