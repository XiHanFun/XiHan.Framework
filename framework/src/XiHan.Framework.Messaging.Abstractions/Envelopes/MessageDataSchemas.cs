// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Envelopes;

/// <summary>
/// 按 CloudEvents <c>type</c> 登记的数据模式
/// </summary>
/// <remarks>
/// 框架自己构造的信封（领域事件、调度与工作流的事件）按自己的 <c>type</c> 查这张表，查到即带上这个 <c>dataschema</c>（RFC-0009「框架事件的数据模式」，ADR-0323）。由 <see cref="MessagingServiceCollectionExtensions.AddMessageDataSchema"/> 与配置节 <c>XiHan:Messaging:DataSchemas</c>（<see cref="MessagingOptions.DataSchemas"/>，消息 Provider 装配时登记，ADR-0332）在装配期填充、以实例注册为单例；没有登记任何模式时容器里没有它。
/// 有界（10000 个 <c>type</c>），装配期写入、发布时并发读取；应用自己构造的信封与发布者都不按它补
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class MessageDataSchemas
{
    /// <summary>
    /// 最多登记的 <c>type</c> 数
    /// </summary>
    internal const int Capacity = 10_000;

    /// <summary>
    /// <c>type</c> 到数据模式
    /// </summary>
    /// <remarks>
    /// 按序数比较，与 CloudEvents 的 <c>type</c> 一样区分大小写
    /// </remarks>
    private readonly ConcurrentDictionary<string, string> _schemas = new(StringComparer.Ordinal);

    /// <summary>
    /// 容量
    /// </summary>
    private readonly int _capacity;

    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="capacity">最多登记的 <c>type</c> 数</param>
    internal MessageDataSchemas(int capacity = Capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
    }

    /// <summary>
    /// 已登记的 <c>type</c> 数
    /// </summary>
    public int Count => _schemas.Count;

    /// <summary>
    /// 按 CloudEvents <c>type</c> 查数据模式
    /// </summary>
    /// <remarks>
    /// 没有登记时无值
    /// </remarks>
    public Maybe<string> Find(string type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return _schemas.TryGetValue(type, out var dataSchema) ? Maybe.Some(dataSchema) : default;
    }

    /// <summary>
    /// 登记一个 <c>type</c> 的数据模式
    /// </summary>
    /// <remarks>
    /// <paramref name="type"/> 照 <see cref="MessageEnvelope.Type"/>、<paramref name="dataSchema"/> 照 <see cref="MessageEnvelope.DataSchema"/>：不能为空或只有空白，不核对 URI 语法；两者都不能有孤立的代理项，写不进结构化 JSON 与 binary 的头。
    /// 同一个 <c>type</c> 再登记同一个值只记一次，另一个值抛出 <see cref="ArgumentException"/>；满了再登记新的 <c>type</c> 抛出 <see cref="InvalidOperationException"/>
    /// </remarks>
    internal void Add(string type, string dataSchema)
    {
        Validate(type, dataSchema);

        // 先查容量再写入：两步不是原子的，装配期是单线程的，这里只防止无界增长
        if (!_schemas.ContainsKey(type) && _schemas.Count >= _capacity)
        {
            throw new InvalidOperationException($"The data schema registry has reached its capacity of {_capacity} event types.");
        }

        // 同一个值是重复的装配调用，只记一次；另一个值不取先登记的也不取后登记的，两处对同一个 type 有分歧是装配错误
        var registered = _schemas.GetOrAdd(type, dataSchema);
        if (!string.Equals(registered, dataSchema, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Event type '{type}' already has data schema '{registered}'; it cannot also have '{dataSchema}'.", nameof(dataSchema));
        }
    }

    /// <summary>
    /// 核对一个 <c>type</c> 与它的数据模式
    /// </summary>
    /// <remarks>
    /// 登记（<see cref="Add"/>）与从配置读（<see cref="MessagingOptions.DataSchemas"/>）共用这一条：<paramref name="type"/> 照 <see cref="MessageEnvelope.Type"/>、<paramref name="dataSchema"/> 照 <see cref="MessageEnvelope.DataSchema"/>，不能为空或只有空白；两者都不能有孤立的代理项。不合抛出 <see cref="ArgumentException"/>
    /// </remarks>
    internal static void Validate(string type, string dataSchema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentNullException.ThrowIfNull(dataSchema);
        if (string.IsNullOrWhiteSpace(dataSchema))
        {
            throw new ArgumentException("The data schema must not be empty.", nameof(dataSchema));
        }

        // 登记时就拒：写不出的值要到每次发布才失败，框架事件的发布失败只记日志
        if (UnicodeText.HasLoneSurrogate(type))
        {
            throw new ArgumentException("The event type must not contain lone surrogates.", nameof(type));
        }

        if (UnicodeText.HasLoneSurrogate(dataSchema))
        {
            throw new ArgumentException($"The data schema for event type '{type}' must not contain lone surrogates.", nameof(dataSchema));
        }
    }
}
