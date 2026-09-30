// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using XiHan.Framework.Caching.Distributed.Abstracts;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Timing;
using XiHan.Framework.Workflow.Events;

namespace XiHan.Framework.Workflow.SqlSugar.Tests;

/// <summary>
/// 可拨动测试时钟
/// </summary>
internal sealed class TestClock : IClock
{
    private DateTime _now = new(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// 当前时间
    /// </summary>
    public DateTime Now => _now;

    /// <summary>
    /// 时间类型，固定为 UTC
    /// </summary>
    public DateTimeKind Kind => DateTimeKind.Utc;

    /// <summary>
    /// 是否支持多时区，固定为不支持
    /// </summary>
    public bool SupportsMultipleTimezone => false;

    /// <summary>
    /// 拨动时钟
    /// </summary>
    /// <param name="duration">前进时长</param>
    public void Advance(TimeSpan duration)
    {
        _now = _now.Add(duration);
    }

    /// <summary>
    /// 规范化时间，原样返回
    /// </summary>
    /// <param name="dateTime">时间</param>
    /// <returns>原时间</returns>
    public DateTime Normalize(DateTime dateTime)
    {
        return dateTime;
    }

    /// <summary>
    /// 转换为用户时间，原样返回
    /// </summary>
    /// <param name="utcDateTime">UTC 时间</param>
    /// <returns>原时间</returns>
    public DateTime ConvertToUserTime(DateTime utcDateTime)
    {
        return utcDateTime;
    }

    /// <summary>
    /// 转换为用户时间，原样返回
    /// </summary>
    /// <param name="dateTimeOffset">时间偏移</param>
    /// <returns>原时间</returns>
    public DateTimeOffset ConvertToUserTime(DateTimeOffset dateTimeOffset)
    {
        return dateTimeOffset;
    }

    /// <summary>
    /// 转换为 UTC 时间，原样返回
    /// </summary>
    /// <param name="dateTime">时间</param>
    /// <returns>原时间</returns>
    public DateTime ConvertToUtc(DateTime dateTime)
    {
        return dateTime;
    }
}

/// <summary>
/// 可切换的测试租户
/// </summary>
internal sealed class TestCurrentTenant : ICurrentTenant
{
    private readonly AsyncLocal<long?> _id = new();

    /// <summary>
    /// 当前租户是否可用
    /// </summary>
    public bool IsAvailable => Id.HasValue;

    /// <summary>
    /// 当前租户标识
    /// </summary>
    public long? Id => _id.Value;

    /// <summary>
    /// 当前租户名称，固定为 null
    /// </summary>
    public string? Name => null;

    /// <summary>
    /// 临时切换当前租户，释放时恢复
    /// </summary>
    /// <param name="id">租户标识</param>
    /// <param name="name">租户名称，忽略</param>
    /// <returns>恢复上下文的释放器</returns>
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
internal sealed class InProcessTestLock : IDistributedLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();

    /// <summary>
    /// 尝试获取锁，单次不阻塞不重试
    /// </summary>
    /// <param name="resourceKey">资源键</param>
    /// <param name="expiry">锁过期时间，忽略</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>锁句柄，获取失败返回 null</returns>
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

/// <summary>
/// 丢弃全部事件的工作流事件发布器
/// </summary>
internal sealed class NullWorkflowEventPublisher : IWorkflowEventPublisher
{
    /// <summary>
    /// 发布事件，不做任何事
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="eventData">事件数据</param>
    /// <returns>已完成的任务</returns>
    public Task PublishAsync<TEvent>(TEvent eventData) where TEvent : class
    {
        return Task.CompletedTask;
    }
}
