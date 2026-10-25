// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;

namespace XiHan.Framework.Workflow.InMemory;

/// <summary>
/// 进程内实例锁：每个实例一个信号量，没人用时回收；等待超过 <see cref="WorkflowOptions.LockTimeout"/> → <see cref="WorkflowErrors.LockTimeout"/>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryWorkflowInstanceLock : IWorkflowInstanceLock, IDisposable
{
    /// <summary>
    /// 保护表的锁。
    /// </summary>
    private readonly Lock _sync = new();

    /// <summary>
    /// 实例 ID → 条目。
    /// </summary>
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    /// <summary>
    /// 等待上限。
    /// </summary>
    private readonly TimeSpan _timeout;

    /// <summary>
    /// 创建。
    /// </summary>
    public InMemoryWorkflowInstanceLock(WorkflowOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _timeout = options.LockTimeout;
    }

    /// <summary>
    /// 当前有人持有或等待的实例数。
    /// </summary>
    public int ActiveCount
    {
        get
        {
            lock (_sync)
            {
                return _entries.Count;
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<IAsyncDisposable>> AcquireAsync(string instanceId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        Entry entry;
        lock (_sync)
        {
            if (!_entries.TryGetValue(instanceId, out entry!))
            {
                entry = new Entry();
                _entries[instanceId] = entry;
            }

            // 先计数再等待：释放方据此判断能否回收条目
            entry.Users++;
        }

        bool acquired;
        try
        {
            acquired = await entry.Semaphore.WaitAsync(_timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Leave(instanceId, entry);
            throw;
        }

        if (!acquired)
        {
            Leave(instanceId, entry);
            return new Error(WorkflowErrors.LockTimeout, WorkflowMessages.LockTimeout, MessageArguments.Of(("id", instanceId)));
        }

        return new Release(this, instanceId, entry);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_sync)
        {
            foreach (var entry in _entries.Values)
            {
                entry.Semaphore.Dispose();
            }

            _entries.Clear();
        }
    }

    /// <summary>
    /// 一个使用者离开：最后一个走时回收条目。
    /// </summary>
    private void Leave(string instanceId, Entry entry)
    {
        lock (_sync)
        {
            entry.Users--;
            if (entry.Users == 0 && _entries.TryGetValue(instanceId, out var current) && ReferenceEquals(current, entry))
            {
                _entries.Remove(instanceId);
                entry.Semaphore.Dispose();
            }
        }
    }

    /// <summary>
    /// 一个实例的信号量与使用者计数。
    /// </summary>
    private sealed class Entry
    {
        /// <summary>信号量（一次一个）。</summary>
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        /// <summary>持有或等待的人数。</summary>
        public int Users { get; set; }
    }

    /// <summary>
    /// 释放句柄：释放信号量并离开。
    /// </summary>
    private sealed class Release(InMemoryWorkflowInstanceLock owner, string instanceId, Entry entry) : IAsyncDisposable
    {
        /// <summary>
        /// 已释放。
        /// </summary>
        private bool _released;

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            if (_released)
            {
                return ValueTask.CompletedTask;
            }

            _released = true;
            entry.Semaphore.Release();
            owner.Leave(instanceId, entry);
            return ValueTask.CompletedTask;
        }
    }
}
