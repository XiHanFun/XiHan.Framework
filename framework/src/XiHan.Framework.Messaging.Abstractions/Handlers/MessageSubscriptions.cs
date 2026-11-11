// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Handlers;

/// <summary>
/// 订阅表：主题与订阅方式到处理器类型
/// </summary>
/// <remarks>
/// 按（主题，方式）分组：同一主题的竞争订阅与广播订阅各有自己的处理器列表，代理各消费一份（RFC-0009「订阅方式」）。在装配时填充，之后只读
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class MessageSubscriptions
{
    /// <summary>
    /// 订阅到处理器类型列表的映射
    /// </summary>
    /// <remarks>
    /// 列表保持订阅顺序
    /// </remarks>
    private readonly Dictionary<MessageSubscription, List<Type>> _handlers = [];

    /// <summary>
    /// 订阅，按首次登记的顺序
    /// </summary>
    private readonly List<MessageSubscription> _subscriptions = [];

    /// <summary>
    /// 是否已冻结
    /// </summary>
    /// <remarks>
    /// 消息运行时与派发器取用后为真，之后不再接受登记
    /// </remarks>
    private volatile bool _frozen;

    /// <summary>
    /// 全部订阅，按首次登记的顺序
    /// </summary>
    /// <remarks>
    /// 消息运行时把它交给代理消费；返回只读视图
    /// </remarks>
    public IReadOnlyList<MessageSubscription> All => _subscriptions.AsReadOnly();

    /// <summary>
    /// 已订阅的主题，任一订阅方式都算
    /// </summary>
    /// <remarks>
    /// 按首次登记的顺序，不重复
    /// </remarks>
    public IReadOnlyCollection<string> Topics => _subscriptions.Select(static s => s.Topic).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>
    /// 主题在该订阅方式下的处理器类型，按订阅顺序
    /// </summary>
    /// <remarks>
    /// 没有订阅时为空
    /// </remarks>
    public IReadOnlyList<Type> HandlersFor(string topic, SubscriptionMode mode)
    {
        ArgumentNullException.ThrowIfNull(topic);
        return TopicName.IsValid(topic) && _handlers.TryGetValue(new MessageSubscription(topic, mode), out var handlers) ? handlers.AsReadOnly() : [];
    }

    /// <summary>
    /// 以给定方式订阅
    /// </summary>
    /// <remarks>
    /// 只在装配时调用；消息运行时或派发器取用之后登记抛出 <see cref="InvalidOperationException"/>
    /// </remarks>
    public MessageSubscriptions Add(string topic, Type handlerType, SubscriptionMode mode)
    {
        ArgumentNullException.ThrowIfNull(handlerType);

        // 冻结后再登记会让运行中的消费与派发看到不一致的订阅表，而且这张表不是线程安全的
        if (_frozen)
        {
            throw new InvalidOperationException("Message subscriptions are read-only once the messaging runtime has started; register them while configuring the features.");
        }

        var subscription = new MessageSubscription(topic, mode);
        if (!_handlers.TryGetValue(subscription, out var handlers))
        {
            handlers = [];
            _handlers[subscription] = handlers;
            _subscriptions.Add(subscription);
        }

        // 同一处理器以同一方式重复订阅同一主题只记一次，避免一条消息被同一处理器处理两遍；
        // 以另一种方式订阅是另一份消费，照常登记
        if (!handlers.Contains(handlerType))
        {
            handlers.Add(handlerType);
        }

        return this;
    }

    /// <summary>
    /// 冻结并返回自身
    /// </summary>
    /// <remarks>
    /// 消息运行时与派发器在构造时调用：此后只读，可被并发读取
    /// </remarks>
    internal MessageSubscriptions Frozen()
    {
        _frozen = true;
        return this;
    }
}
