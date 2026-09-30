// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using XiHan.Framework.Uow;

namespace XiHan.Framework.Web.Api.Idempotency;

/// <summary>
/// 进程内有界幂等存储
/// </summary>
/// <remarks>
/// 记录只存在于当前进程，进程退出后丢失，也不跨实例共享。
/// 处理中记录不自动过期；完成与不确定记录超过保留期后可被重新取得。
/// 在未完成的事务型工作单元内写入完成时，快照先行保存、对外仍为处理中，工作单元提交成功后才转为已完成。
/// </remarks>
public class DefaultIdempotencyStore : IIdempotencyStore
{
    private readonly Lock _lock = new();
    private readonly Dictionary<IdempotencyRecordKey, Entry> _entries = [];
    private readonly XiHanIdempotencyOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private long _totalResponseBytes;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">幂等配置</param>
    /// <param name="timeProvider">时钟</param>
    /// <param name="unitOfWorkManager">工作单元管理器</param>
    public DefaultIdempotencyStore(IOptions<XiHanIdempotencyOptions> options, TimeProvider timeProvider, IUnitOfWorkManager unitOfWorkManager)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
        _unitOfWorkManager = unitOfWorkManager;
    }

    /// <inheritdoc />
    public Task<IdempotencyAcquireResult> TryAcquireAsync(IdempotencyRecordKey key, string fingerprint, bool isTransactional, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();
        var now = _timeProvider.GetUtcNow();

        lock (_lock)
        {
            if (_entries.TryGetValue(key, out var existing))
            {
                if (IsExpired(existing, now))
                {
                    RemoveEntry(key, existing);
                }
                else
                {
                    return Task.FromResult(ResolveExisting(existing, fingerprint));
                }
            }

            if (IsFull())
            {
                PurgeExpired(now);
                if (IsFull())
                {
                    return Task.FromResult(IdempotencyAcquireResult.CapacityExceeded());
                }
            }

            var ownerToken = Guid.NewGuid();
            _entries[key] = new Entry(fingerprint, ownerToken);
            return Task.FromResult(IdempotencyAcquireResult.Acquired(ownerToken));
        }
    }

    /// <inheritdoc />
    public Task CompleteAsync(IdempotencyRecordKey key, Guid ownerToken, StoredResponse response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(response);

        var now = _timeProvider.GetUtcNow();
        var unitOfWork = _unitOfWorkManager.Current;
        var defersToCommit = unitOfWork is { IsCompleted: false, Options.IsTransactional: true };

        lock (_lock)
        {
            if (!_entries.TryGetValue(key, out var entry) ||
                entry.OwnerToken != ownerToken ||
                entry.State != EntryState.Processing ||
                entry.Response is not null)
            {
                throw new InvalidOperationException("幂等记录不存在、拥有者令牌不符或已不是处理中状态，无法写入完成。");
            }

            var size = response.Body?.Length ?? 0;
            if (_totalResponseBytes + size > _options.MaxTotalResponseBytes)
            {
                PurgeExpired(now);
                if (_totalResponseBytes + size > _options.MaxTotalResponseBytes)
                {
                    throw new InvalidOperationException("幂等存储的响应快照容量已满，无法写入完成。");
                }
            }

            entry.Response = response;
            _totalResponseBytes += size;
            if (!defersToCommit)
            {
                MarkCompleted(entry, now);
            }
        }

        if (defersToCommit)
        {
            unitOfWork!.OnCompleted(() =>
            {
                lock (_lock)
                {
                    if (_entries.TryGetValue(key, out var entry) &&
                        entry.OwnerToken == ownerToken &&
                        entry.State == EntryState.Processing &&
                        entry.Response is not null)
                    {
                        MarkCompleted(entry, _timeProvider.GetUtcNow());
                    }
                }

                return Task.CompletedTask;
            });
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>释放会删除该令牌持有的记录，不论其状态。</remarks>
    public Task ReleaseAsync(IdempotencyRecordKey key, Guid ownerToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        lock (_lock)
        {
            if (_entries.TryGetValue(key, out var entry) && entry.OwnerToken == ownerToken)
            {
                RemoveEntry(key, entry);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task MarkIndeterminateAsync(IdempotencyRecordKey key, Guid ownerToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        lock (_lock)
        {
            if (_entries.TryGetValue(key, out var entry) &&
                entry.OwnerToken == ownerToken &&
                entry.State == EntryState.Processing)
            {
                entry.State = EntryState.Indeterminate;
                entry.ExpiresAt = _timeProvider.GetUtcNow().Add(_options.CompletedRetention);
            }
        }

        return Task.CompletedTask;
    }

    private static IdempotencyAcquireResult ResolveExisting(Entry entry, string fingerprint)
    {
        if (!string.Equals(entry.Fingerprint, fingerprint, StringComparison.Ordinal))
        {
            return IdempotencyAcquireResult.Conflict();
        }

        return entry.State switch
        {
            EntryState.Completed => IdempotencyAcquireResult.Replay(entry.Response!),
            EntryState.Processing => IdempotencyAcquireResult.InProgress(),
            _ => IdempotencyAcquireResult.Indeterminate()
        };
    }

    private static bool IsExpired(Entry entry, DateTimeOffset now)
    {
        return entry.State != EntryState.Processing && entry.ExpiresAt <= now;
    }

    private void MarkCompleted(Entry entry, DateTimeOffset now)
    {
        entry.State = EntryState.Completed;
        entry.ExpiresAt = now.Add(_options.CompletedRetention);
    }

    private bool IsFull()
    {
        return _entries.Count >= _options.MaxEntries || _totalResponseBytes >= _options.MaxTotalResponseBytes;
    }

    private void PurgeExpired(DateTimeOffset now)
    {
        var expired = _entries
            .Where(pair => IsExpired(pair.Value, now))
            .ToList();

        foreach (var (key, entry) in expired)
        {
            RemoveEntry(key, entry);
        }
    }

    private void RemoveEntry(IdempotencyRecordKey key, Entry entry)
    {
        _entries.Remove(key);
        _totalResponseBytes -= entry.Response?.Body?.Length ?? 0;
    }

    private enum EntryState
    {
        Processing,
        Completed,
        Indeterminate
    }

    private sealed class Entry(string fingerprint, Guid ownerToken)
    {
        public string Fingerprint { get; } = fingerprint;

        public Guid OwnerToken { get; } = ownerToken;

        public EntryState State { get; set; } = EntryState.Processing;

        public StoredResponse? Response { get; set; }

        public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.MaxValue;
    }
}
