// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;

namespace XiHan.Framework.Web.Api.Idempotency;

/// <summary>
/// 进程内有界幂等存储
/// </summary>
/// <remarks>
/// 记录只存在于当前进程，进程退出后丢失，也不跨实例共享；需要跨实例一致性时应替换为落库存储。
/// 处理中与不确定记录不自动过期；完成记录超过保留期后可被重新取得。
/// </remarks>
public class DefaultIdempotencyStore : IIdempotencyStore
{
    private readonly Lock _lock = new();
    private readonly Dictionary<IdempotencyRecordKey, Entry> _entries = [];
    private readonly XiHanIdempotencyOptions _options;
    private readonly TimeProvider _timeProvider;
    private long _totalResponseBytes;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">幂等配置</param>
    /// <param name="timeProvider">时钟</param>
    public DefaultIdempotencyStore(IOptions<XiHanIdempotencyOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
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
                if (existing.State == EntryState.Completed && existing.ExpiresAt <= now)
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

        lock (_lock)
        {
            if (!_entries.TryGetValue(key, out var entry) ||
                entry.OwnerToken != ownerToken ||
                entry.State != EntryState.Processing)
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

            entry.State = EntryState.Completed;
            entry.Response = response;
            entry.ExpiresAt = now.Add(_options.CompletedRetention);
            _totalResponseBytes += size;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>本存储不参与事务，释放会删除该令牌持有的记录，不论其状态。</remarks>
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

    private bool IsFull()
    {
        return _entries.Count >= _options.MaxEntries || _totalResponseBytes >= _options.MaxTotalResponseBytes;
    }

    private void PurgeExpired(DateTimeOffset now)
    {
        var expired = _entries
            .Where(pair => pair.Value.State == EntryState.Completed && pair.Value.ExpiresAt <= now)
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
