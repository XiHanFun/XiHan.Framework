// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using XiHan.Framework.Caching.Distributed.Abstracts;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Timing;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Abstractions.Stores;
using XiHan.Framework.Workflow.Abstractions.UserTasks;
using XiHan.Framework.Workflow.Events;
using XiHan.Framework.Workflow.Extensions.DependencyInjection;

namespace XiHan.Framework.Workflow.Tests;

/// <summary>
/// 工作流测试主机（构建含全部桩件的服务容器）
/// </summary>
public sealed class WorkflowTestHost : IDisposable
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="configureServices">追加服务注册委托</param>
    public WorkflowTestHost(Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));

        Clock = new TestClock();
        Lock = new InProcessTestLock();
        Events = new RecordingEventPublisher();
        services.AddSingleton<IClock>(Clock);
        services.AddSingleton<ICurrentTenant>(new TestCurrentTenant());
        services.AddSingleton<IDistributedLock>(Lock);
        services.AddSingleton(IdGeneratorFactory.CreateSnowflakeIdGenerator_LowWorkload());

        services.AddXiHanWorkflow(configuration);
        services.Replace(ServiceDescriptor.Singleton<IWorkflowEventPublisher>(Events));
        configureServices?.Invoke(services);

        Provider = services.BuildServiceProvider();
    }

    /// <summary>
    /// 服务提供者
    /// </summary>
    public ServiceProvider Provider { get; }

    /// <summary>
    /// 可拨动测试时钟
    /// </summary>
    public TestClock Clock { get; }

    /// <summary>
    /// 进程内测试锁（供锁竞争测试手动占用）
    /// </summary>
    public InProcessTestLock Lock { get; }

    /// <summary>
    /// 记录型事件发布器（断言生命周期事件）
    /// </summary>
    public RecordingEventPublisher Events { get; }

    /// <summary>
    /// 工作流引擎
    /// </summary>
    public IWorkflowEngine Engine => Provider.GetRequiredService<IWorkflowEngine>();

    /// <summary>
    /// 定义管理器
    /// </summary>
    public IWorkflowDefinitionManager DefinitionManager => Provider.GetRequiredService<IWorkflowDefinitionManager>();

    /// <summary>
    /// 人工任务服务
    /// </summary>
    public IWorkflowUserTaskService UserTaskService => Provider.GetRequiredService<IWorkflowUserTaskService>();

    /// <summary>
    /// 实例存储
    /// </summary>
    public IWorkflowInstanceStore InstanceStore => Provider.GetRequiredService<IWorkflowInstanceStore>();

    /// <summary>
    /// 书签存储
    /// </summary>
    public IWorkflowBookmarkStore BookmarkStore => Provider.GetRequiredService<IWorkflowBookmarkStore>();

    /// <summary>
    /// 创建并发布定义
    /// </summary>
    /// <param name="definition">定义内容</param>
    /// <returns>已发布定义</returns>
    public async Task<WorkflowDefinition> PublishAsync(WorkflowDefinition definition)
    {
        var created = await DefinitionManager.CreateAsync(definition);
        return await DefinitionManager.PublishAsync(created.Id);
    }

    /// <summary>
    /// 重新加载实例最新状态
    /// </summary>
    /// <param name="instanceId">实例标识</param>
    /// <returns>实例</returns>
    public async Task<WorkflowInstance> ReloadAsync(string instanceId)
    {
        return await InstanceStore.FindAsync(instanceId)
            ?? throw new InvalidOperationException($"实例 {instanceId} 不存在");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Provider.Dispose();
    }
}

/// <summary>
/// 记录型事件发布器（把全部事件按发布顺序记录，供测试断言）
/// </summary>
public sealed class RecordingEventPublisher : IWorkflowEventPublisher
{
    private readonly ConcurrentQueue<object> _events = new();

    /// <summary>
    /// 已发布事件（按发布顺序）
    /// </summary>
    public IReadOnlyList<object> Published => [.. _events];

    /// <inheritdoc />
    public Task PublishAsync<TEvent>(TEvent eventData) where TEvent : class
    {
        _events.Enqueue(eventData);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取指定类型的事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <returns>事件列表</returns>
    public List<TEvent> OfType<TEvent>()
    {
        return [.. _events.OfType<TEvent>()];
    }
}

/// <summary>
/// 可拨动测试时钟
/// </summary>
public sealed class TestClock : IClock
{
    private DateTime _now = new(2026, 7, 16, 8, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    public DateTime Now => _now;

    /// <inheritdoc />
    public DateTimeKind Kind => DateTimeKind.Utc;

    /// <inheritdoc />
    public bool SupportsMultipleTimezone => false;

    /// <summary>
    /// 拨动时钟
    /// </summary>
    /// <param name="duration">前进时长</param>
    public void Advance(TimeSpan duration)
    {
        _now = _now.Add(duration);
    }

    /// <inheritdoc />
    public DateTime Normalize(DateTime dateTime)
    {
        return dateTime;
    }

    /// <inheritdoc />
    public DateTime ConvertToUserTime(DateTime utcDateTime)
    {
        return utcDateTime;
    }

    /// <inheritdoc />
    public DateTimeOffset ConvertToUserTime(DateTimeOffset dateTimeOffset)
    {
        return dateTimeOffset;
    }

    /// <inheritdoc />
    public DateTime ConvertToUtc(DateTime dateTime)
    {
        return dateTime;
    }
}

/// <summary>
/// 测试租户
/// </summary>
public sealed class TestCurrentTenant : ICurrentTenant
{
    private readonly AsyncLocal<long?> _id = new();

    /// <inheritdoc />
    public bool IsAvailable => Id.HasValue;

    /// <inheritdoc />
    public long? Id => _id.Value;

    /// <inheritdoc />
    public string? Name => null;

    /// <inheritdoc />
    public IDisposable Change(long? id, string? name = null)
    {
        var previous = _id.Value;
        _id.Value = id;
        return new RestoreScope(() => _id.Value = previous);
    }

    private sealed class RestoreScope : IDisposable
    {
        private readonly Action _restore;

        public RestoreScope(Action restore)
        {
            _restore = restore;
        }

        public void Dispose()
        {
            _restore();
        }
    }
}

/// <summary>
/// 进程内测试分布式锁
/// </summary>
public sealed class InProcessTestLock : IDistributedLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();

    /// <inheritdoc />
    public async Task<IDistributedLockHandle?> TryAcquireAsync(string resourceKey, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var semaphore = _semaphores.GetOrAdd(resourceKey, _ => new SemaphoreSlim(1, 1));
        var acquired = await semaphore.WaitAsync(TimeSpan.Zero, cancellationToken);
        return acquired ? new Handle(resourceKey, semaphore) : null;
    }

    private sealed class Handle : IDistributedLockHandle
    {
        private readonly SemaphoreSlim _semaphore;
        private int _released;

        public Handle(string resourceKey, SemaphoreSlim semaphore)
        {
            ResourceKey = resourceKey;
            _semaphore = semaphore;
        }

        public string ResourceKey { get; }

        public string LockId { get; } = Guid.NewGuid().ToString("N");

        public bool IsReleased => _released == 1;

        public Task ReleaseAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                _semaphore.Release();
            }

            return Task.CompletedTask;
        }

        public Task<bool> ExtendAsync(TimeSpan expiry, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(!IsReleased);
        }

        public void Dispose()
        {
            ReleaseAsync().GetAwaiter().GetResult();
        }

        public ValueTask DisposeAsync()
        {
            return new ValueTask(ReleaseAsync());
        }
    }
}
