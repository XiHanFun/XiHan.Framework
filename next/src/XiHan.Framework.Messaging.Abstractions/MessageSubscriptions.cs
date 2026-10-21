// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions;

/// <summary>
/// 主题到处理器类型的订阅表。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class MessageSubscriptions
{
    /// <summary>
    /// 主题到处理器类型列表的映射；列表保持订阅顺序。
    /// </summary>
    private readonly Dictionary<string, List<Type>> _handlers = new(StringComparer.Ordinal);

    /// <summary>
    /// 已订阅的主题。
    /// </summary>
    public IReadOnlyCollection<string> Topics => _handlers.Keys;

    /// <summary>
    /// 主题的处理器类型，按订阅顺序。
    /// </summary>
    public IReadOnlyList<Type> HandlersFor(string topic)
    {
        ArgumentNullException.ThrowIfNull(topic);
        return _handlers.TryGetValue(topic, out var handlers) ? handlers : [];
    }

    /// <summary>
    /// 订阅。
    /// </summary>
    public MessageSubscriptions Add(string topic, Type handlerType)
    {
        ArgumentNullException.ThrowIfNull(handlerType);
        TopicName.Validate(topic);
        if (!_handlers.TryGetValue(topic, out var handlers))
        {
            handlers = [];
            _handlers[topic] = handlers;
        }

        // 同一处理器重复订阅同一主题只记一次，避免一条消息被同一处理器处理两遍
        if (!handlers.Contains(handlerType))
        {
            handlers.Add(handlerType);
        }

        return this;
    }
}
