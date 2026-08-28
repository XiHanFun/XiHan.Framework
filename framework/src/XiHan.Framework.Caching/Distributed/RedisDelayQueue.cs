// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using StackExchange.Redis;
using System.Text.Json;
using XiHan.Framework.Caching.Distributed.Abstracts;

namespace XiHan.Framework.Caching.Distributed;

/// <summary>
/// <see cref="IRedisDelayQueue{T}"/> 的实现：Redis Sorted Set 承载延迟消息，score 为到期时间戳(ms)。
/// </summary>
/// <remarks>
/// - 键：<c>default:delay:{类型全名}</c>；成员为 <c>{唯一前缀}|{JSON}</c>（前缀避免相同载荷被 ZADD 去重）。
/// - 入队：ZADD（score=到期 ms）。领取：Lua 原子 ZRANGEBYSCORE(-inf, now) + ZREM，多消费者不重复领取。
/// 注册为单例（每个封闭类型一个实例）。
/// </remarks>
/// <typeparam name="T">消息类型</typeparam>
public sealed class RedisDelayQueue<T> : IRedisDelayQueue<T>
{
    // 原子领取到期项：取 score≤now 的前 count 个成员并删除，返回这些成员
    private const string DequeueDueScript =
        """
        local due = redis.call('ZRANGEBYSCORE', KEYS[1], '-inf', ARGV[1], 'LIMIT', 0, ARGV[2])
        if #due > 0 then
            redis.call('ZREM', KEYS[1], unpack(due))
        end
        return due
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly string TypeKey = typeof(T).FullName ?? typeof(T).Name;
    private static readonly RedisKey ZSetKey = $"default:delay:{TypeKey}";

    private readonly IConnectionMultiplexer _connection;

    /// <summary>
    /// 构造函数
    /// </summary>
    public RedisDelayQueue(IConnectionMultiplexer connection)
    {
        _connection = connection;
    }

    /// <summary>
    /// 延迟入队，消息在 <paramref name="delay"/> 之后才可被取出
    /// </summary>
    /// <param name="item">消息</param>
    /// <param name="delay">延迟时长</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task EnqueueAsync(T item, TimeSpan delay, CancellationToken cancellationToken = default)
    {
        return EnqueueAtAsync(item, DateTimeOffset.UtcNow.Add(delay), cancellationToken);
    }

    /// <summary>
    /// 定时入队，消息到达 <paramref name="dueTime"/> 后才可被取出
    /// </summary>
    /// <param name="item">消息</param>
    /// <param name="dueTime">到期时刻</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task EnqueueAtAsync(T item, DateTimeOffset dueTime, CancellationToken cancellationToken = default)
    {
        // 唯一前缀确保相同载荷也作为不同成员（否则 ZADD 会按成员去重）
        var member = $"{Guid.NewGuid():N}|{JsonSerializer.Serialize(item, JsonOptions)}";
        return _connection.GetDatabase().SortedSetAddAsync(ZSetKey, member, dueTime.ToUnixTimeMilliseconds());
    }

    /// <summary>
    /// 原子取出已到期的消息，最多 <paramref name="count"/> 条，取出的消息同时从队列移除
    /// </summary>
    /// <param name="count">最大取出条数，小于等于零时返回空集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已到期的消息集合</returns>
    public async Task<IReadOnlyList<T>> DequeueDueAsync(int count, CancellationToken cancellationToken = default)
    {
        if (count <= 0)
        {
            return [];
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var raw = await _connection.GetDatabase()
            .ScriptEvaluateAsync(DequeueDueScript, [ZSetKey], [now, count]);
        if (raw.IsNull)
        {
            return [];
        }

        var members = (RedisValue[]?)raw;
        if (members is null || members.Length == 0)
        {
            return [];
        }

        var result = new List<T>(members.Length);
        foreach (var member in members)
        {
            var item = Deserialize(member);
            if (item is not null)
            {
                result.Add(item);
            }
        }

        return result;
    }

    /// <summary>
    /// 获取队列消息总数，含未到期的消息
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>队列中的消息条数</returns>
    public Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return _connection.GetDatabase().SortedSetLengthAsync(ZSetKey);
    }

    private static T? Deserialize(RedisValue member)
    {
        var text = member.ToString();
        var separator = text.IndexOf('|');
        var json = separator >= 0 ? text[(separator + 1)..] : text;
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }
}
