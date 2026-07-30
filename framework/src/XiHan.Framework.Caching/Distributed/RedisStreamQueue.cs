// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using StackExchange.Redis;
using System.Text.Json;
using XiHan.Framework.Caching.Distributed.Abstracts;

namespace XiHan.Framework.Caching.Distributed;

/// <summary>
/// <see cref="IRedisStreamQueue{T}"/> 的实现：Redis Streams 承载消息，消费组 + ACK + XCLAIM 提供可靠投递。
/// </summary>
/// <remarks>
/// - 键：<c>xihan:stream:{类型全名}</c>；唯一消费组 <c>xihan</c>；唤醒频道 <c>xihan:stream-wake:{类型全名}</c>。
/// - 入队 XADD；消费 XREADGROUP（"&gt;" 读新消息，进 PEL）；确认 XACK；重投 XPENDING 筛空闲 + XCLAIM。
/// - 等待：首次惰性订阅唤醒频道 + 本地信号量合并；被入队唤醒或超时返回，空闲不轮询。
/// 注册为单例（每个封闭类型一个实例）。
/// </remarks>
/// <typeparam name="T">消息类型</typeparam>
public sealed class RedisStreamQueue<T> : IRedisStreamQueue<T>, IDisposable
{
    private const string DataField = "data";
    private const string GroupName = "xihan";
    private static readonly RedisValue NewMessages = ">";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly string TypeKey = typeof(T).FullName ?? typeof(T).Name;
    private static readonly RedisKey StreamKey = $"xihan:stream:{TypeKey}";
    private static readonly RedisChannel WakeChannel = RedisChannel.Literal($"xihan:stream-wake:{TypeKey}");

    private readonly IConnectionMultiplexer _connection;
    private readonly SemaphoreSlim _signal = new(0, 1);
    private int _groupEnsured;
    private int _subscribed;

    /// <summary>
    /// 构造函数
    /// </summary>
    public RedisStreamQueue(IConnectionMultiplexer connection)
    {
        _connection = connection;
    }

    /// <inheritdoc />
    public async Task<string> EnqueueAsync(T item, CancellationToken cancellationToken = default)
    {
        var id = await _connection.GetDatabase().StreamAddAsync(StreamKey, DataField, Serialize(item));
        await _connection.GetSubscriber().PublishAsync(WakeChannel, RedisValue.EmptyString);
        return id.ToString();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RedisStreamMessage<T>>> ReadAsync(string consumer, int count, CancellationToken cancellationToken = default)
    {
        await EnsureGroupAsync();
        var entries = await _connection.GetDatabase()
            .StreamReadGroupAsync(StreamKey, GroupName, consumer, NewMessages, count);
        return Map(entries, deliveryCount: 1);
    }

    /// <inheritdoc />
    public Task AckAsync(IEnumerable<string> messageIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messageIds);
        var ids = messageIds.Select(id => (RedisValue)id).ToArray();
        return ids.Length == 0
            ? Task.CompletedTask
            : _connection.GetDatabase().StreamAcknowledgeAsync(StreamKey, GroupName, ids);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RedisStreamMessage<T>>> ClaimStaleAsync(string consumer, TimeSpan minIdle, int count, CancellationToken cancellationToken = default)
    {
        await EnsureGroupAsync();
        var db = _connection.GetDatabase();
        var minIdleMs = (long)minIdle.TotalMilliseconds;

        // XPENDING：取本组待确认消息（含空闲时长与已投递次数），筛出空闲超阈值的
        var pending = await db.StreamPendingMessagesAsync(StreamKey, GroupName, count, RedisValue.Null);
        var stale = pending.Where(p => p.IdleTimeInMilliseconds >= minIdleMs).ToArray();
        if (stale.Length == 0)
        {
            return [];
        }

        // XCLAIM：认领给当前消费者（minIdle 再次保证不抢占正在处理的）
        var ids = stale.Select(p => p.MessageId).ToArray();
        var claimed = await db.StreamClaimAsync(StreamKey, GroupName, consumer, minIdleMs, ids);
        var deliveryById = stale.ToDictionary(p => p.MessageId.ToString(), p => (long)p.DeliveryCount);

        var result = new List<RedisStreamMessage<T>>(claimed.Length);
        foreach (var entry in claimed)
        {
            var id = entry.Id.ToString();
            result.Add(new RedisStreamMessage<T>(id, ReadEntry(entry), deliveryById.GetValueOrDefault(id, 1)));
        }

        return result;
    }

    /// <inheritdoc />
    public Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return _connection.GetDatabase().StreamLengthAsync(StreamKey);
    }

    /// <inheritdoc />
    public async Task WaitForSignalAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        await EnsureSubscribedAsync();
        await _signal.WaitAsync(timeout, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _signal.Dispose();
    }

    private static string Serialize(T item)
    {
        return JsonSerializer.Serialize(item, JsonOptions);
    }

    private static IReadOnlyList<RedisStreamMessage<T>> Map(StreamEntry[] entries, long deliveryCount)
    {
        if (entries is null || entries.Length == 0)
        {
            return [];
        }

        var list = new List<RedisStreamMessage<T>>(entries.Length);
        foreach (var entry in entries)
        {
            list.Add(new RedisStreamMessage<T>(entry.Id.ToString(), ReadEntry(entry), deliveryCount));
        }

        return list;
    }

    private static T? ReadEntry(StreamEntry entry)
    {
        var data = entry[DataField];
        return data.IsNullOrEmpty ? default : JsonSerializer.Deserialize<T>(data.ToString(), JsonOptions);
    }

    private async Task EnsureGroupAsync()
    {
        if (Interlocked.Exchange(ref _groupEnsured, 1) != 0)
        {
            return;
        }

        try
        {
            // MKSTREAM：流不存在则建；从头(0)消费，保证不丢已入队的历史消息
            await _connection.GetDatabase()
                .StreamCreateConsumerGroupAsync(StreamKey, GroupName, StreamPosition.Beginning, createStream: true);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP", StringComparison.Ordinal))
        {
            // 组已存在，忽略
        }
    }

    private async Task EnsureSubscribedAsync()
    {
        if (Interlocked.Exchange(ref _subscribed, 1) != 0)
        {
            return;
        }

        await _connection.GetSubscriber().SubscribeAsync(WakeChannel, (_, _) =>
        {
            try
            {
                _signal.Release();
            }
            catch (SemaphoreFullException)
            {
                // 已有一次待处理唤醒，合并即可
            }
        });
    }
}
