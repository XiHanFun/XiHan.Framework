// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using XiHan.Framework.Bot.Telegram.Abstractions;

namespace XiHan.Framework.Bot.Telegram.Stores;

/// <summary>
/// 进程内 Telegram Update 幂等去重器（TTL 字典；多实例部署请以分布式实现覆盖）
/// </summary>
public class InMemoryTelegramUpdateDeduplicator : ITelegramUpdateDeduplicator
{
    private static readonly TimeSpan EntryTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, long> _entries = new(StringComparer.Ordinal);
    private long _lastSweepTicks;

    /// <inheritdoc />
    public Task<bool> TryMarkProcessedAsync(string botName, int updateId, CancellationToken cancellationToken = default)
    {
        SweepIfDue();

        var key = $"{botName}:{updateId}";
        var expiresAtTicks = DateTimeOffset.UtcNow.Add(EntryTtl).UtcTicks;
        return Task.FromResult(_entries.TryAdd(key, expiresAtTicks));
    }

    /// <inheritdoc />
    public Task TryUnmarkAsync(string botName, int updateId, CancellationToken cancellationToken = default)
    {
        _ = _entries.TryRemove($"{botName}:{updateId}", out _);
        return Task.CompletedTask;
    }

    private void SweepIfDue()
    {
        var nowTicks = DateTimeOffset.UtcNow.UtcTicks;
        var lastSweep = Interlocked.Read(ref _lastSweepTicks);
        if (nowTicks - lastSweep < SweepInterval.Ticks)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _lastSweepTicks, nowTicks, lastSweep) != lastSweep)
        {
            return;
        }

        foreach (var entry in _entries)
        {
            if (entry.Value < nowTicks)
            {
                _ = _entries.TryRemove(entry.Key, out _);
            }
        }
    }
}
