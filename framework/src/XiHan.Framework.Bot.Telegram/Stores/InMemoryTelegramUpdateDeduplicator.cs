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

    /// <summary>
    /// 尝试将指定 Update 标记为已处理，标记前顺带清理过期条目
    /// </summary>
    /// <param name="botName">机器人名称</param>
    /// <param name="updateId">Update Id</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>true 表示首次处理（已占位成功）；false 表示重复投递（应跳过）</returns>
    public Task<bool> TryMarkProcessedAsync(string botName, int updateId, CancellationToken cancellationToken = default)
    {
        SweepIfDue();

        var key = $"{botName}:{updateId}";
        var expiresAtTicks = DateTimeOffset.UtcNow.Add(EntryTtl).UtcTicks;
        return Task.FromResult(_entries.TryAdd(key, expiresAtTicks));
    }

    /// <summary>
    /// 移除指定 Update 的幂等标记，允许该 Update 重新被处理
    /// </summary>
    /// <param name="botName">机器人名称</param>
    /// <param name="updateId">Update Id</param>
    /// <param name="cancellationToken">取消令牌</param>
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
