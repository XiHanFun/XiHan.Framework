// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;
using StackExchange.Redis;

namespace XiHan.Framework.Messaging.Redis.Tests;

/// <summary>
/// 打桩的 <see cref="IDatabase"/>：一条流上预置的新条目，记录确认、认领、写入与退回的投递次数
/// </summary>
/// <remarks>
/// 以 <see cref="DispatchProxy"/> 生成，只实现消费循环用到的成员：<c>StreamReadGroupAsync</c> 把预置的条目一次交出、之后为空；<c>StreamAcknowledgeAsync</c>、<c>StreamClaimAsync</c> 与 <c>StreamAddAsync</c> 记下调用；退回投递次数的脚本记下每一对条目 ID 与次数，别的脚本（维护）返回空数组，设了 <see cref="MaintenanceGate"/> 时等它放行；<c>StreamAutoClaimAsync</c> 没有可回收的条目。读取与回收各记下调用的次数。其余成员抛 <see cref="NotSupportedException"/>。不模拟待确认列表与投递次数，回收的行为只在真实 Redis 上验证
/// </remarks>
public class FakeRedisDatabase : DispatchProxy
{
    /// <summary>
    /// 保护记录的锁
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 还没被读走的新条目
    /// </summary>
    private readonly List<StreamEntry> _entries = [];

    /// <summary>
    /// 确认过的条目 ID，按顺序
    /// </summary>
    public List<string> Acknowledged { get; } = [];

    /// <summary>
    /// 认领（原位重试前）过的条目 ID，按顺序
    /// </summary>
    public List<string> Claimed { get; } = [];

    /// <summary>
    /// 退回过投递次数的条目：条目 ID 与消费循环看到的投递次数，按顺序
    /// </summary>
    /// <remarks>
    /// 停止打断的投递与停止时还没派发的条目，脚本把它们的次数设回减一后的值（ADR-0284）
    /// </remarks>
    public List<(string Id, int DeliveryCount)> Restored { get; } = [];

    /// <summary>
    /// <c>StreamReadGroupAsync</c> 被调用的次数
    /// </summary>
    public int Reads { get; private set; }

    /// <summary>
    /// <c>StreamAutoClaimAsync</c> 被调用的次数
    /// </summary>
    public int AutoClaims { get; private set; }

    /// <summary>
    /// 维护脚本的闸门
    /// </summary>
    /// <remarks>
    /// 设了的话维护脚本先标记 <see cref="MaintenanceEntered"/>，等它完成才返回：用例借此在维护途中发出停止信号
    /// </remarks>
    public TaskCompletionSource? MaintenanceGate { get; set; }

    /// <summary>
    /// 维护脚本开始执行的信号
    /// </summary>
    public TaskCompletionSource MaintenanceEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 写入过的条目：流键与字段，按顺序
    /// </summary>
    public List<(string Key, Dictionary<string, string> Fields)> Added { get; } = [];

    /// <summary>
    /// 创建一个预置了新条目的打桩数据库
    /// </summary>
    public static (IDatabase Database, FakeRedisDatabase Fake) Create(params StreamEntry[] entries)
    {
        var database = DispatchProxy.Create<IDatabase, FakeRedisDatabase>();
        var fake = (FakeRedisDatabase)(object)database;
        fake._entries.AddRange(entries);
        return (database, fake);
    }

    /// <summary>
    /// 在锁内读一份记录的快照
    /// </summary>
    public T Read<T>(Func<FakeRedisDatabase, T> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        lock (_gate)
        {
            return read(this);
        }
    }

    /// <inheritdoc />
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        args ??= [];
        lock (_gate)
        {
            switch (targetMethod.Name)
            {
                case "ScriptEvaluateAsync" when args[0] is RedisStreamScripts.RestoreDeliveryCounts && args[2] is RedisValue[] pairs:
                    // 前两项是组名与消费者名，之后每两项一对：条目 ID 与投递次数
                    for (var index = 2; index + 1 < pairs.Length; index += 2)
                    {
                        Restored.Add((pairs[index].ToString(), (int)pairs[index + 1]));
                    }

                    return Task.FromResult(RedisResult.Create((RedisValue)((pairs.Length - 2) / 2)));
                case "ScriptEvaluateAsync":
                    // 维护脚本：没有清理或整理任何东西；有闸门时等它放行，不在锁里等
                    MaintenanceEntered.TrySetResult();
                    var empty = RedisResult.Create(Array.Empty<RedisValue>());
                    return MaintenanceGate is { } gate ? gate.Task.ContinueWith(_ => empty, TaskScheduler.Default) : Task.FromResult(empty);
                case "StreamAutoClaimAsync":
                    AutoClaims++;
                    return Task.FromResult(StreamAutoClaimResult.Null);
                case "StreamReadGroupAsync":
                    Reads++;
                    StreamEntry[] batch = [.. _entries];
                    _entries.Clear();
                    return Task.FromResult(batch);
                case "StreamAcknowledgeAsync" when args[2] is RedisValue id:
                    Acknowledged.Add(id.ToString());
                    return Task.FromResult(1L);
                case "StreamClaimAsync" when args[4] is RedisValue[] ids:
                    Claimed.AddRange(ids.Select(static id => id.ToString()));
                    // 条目仍在待确认列表里：认领成功，交回同样个数的条目
                    return Task.FromResult(ids.Select(static id => new StreamEntry(id, [])).ToArray());
                case "StreamAddAsync" when args[0] is RedisKey key && args[1] is NameValueEntry[] fields:
                    Added.Add((key.ToString(), fields.ToDictionary(static f => f.Name.ToString(), static f => f.Value.ToString(), StringComparer.Ordinal)));
                    return Task.FromResult<RedisValue>("1-0");
                default:
                    throw new NotSupportedException($"The fake database does not support {targetMethod.Name}.");
            }
        }
    }
}
