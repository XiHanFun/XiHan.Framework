// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;
using Confluent.Kafka;

namespace XiHan.Framework.Messaging.Kafka.Tests;

/// <summary>
/// 打桩的生产者。
/// </summary>
/// <remarks>
/// 以 <see cref="DispatchProxy"/> 生成，只实现代理用到的成员：记录每次 <c>ProduceAsync</c> 的主题，按 <see cref="Respond"/> 给出投递报告（可抛异常）。其余成员抛 <see cref="NotSupportedException"/>。
/// </remarks>
public class FakeKafkaProducer : DispatchProxy
{
    /// <summary>
    /// 每次发布的目标主题，按顺序。
    /// </summary>
    public List<string> Topics { get; } = [];

    /// <summary>
    /// 第几次发布（从 0 开始）、主题与消息到投递报告。
    /// </summary>
    /// <remarks>
    /// 缺省全部落盘；抛出的异常即为这次发布的结果。
    /// </remarks>
    public Func<int, string, Message<string?, byte[]>, DeliveryResult<string?, byte[]>> Respond { get; set; } = static (_, topic, message) => Report(topic, message, PersistenceStatus.Persisted);

    /// <summary>
    /// 创建一个打桩生产者。
    /// </summary>
    public static (IProducer<string?, byte[]> Producer, FakeKafkaProducer Fake) Create()
    {
        var producer = DispatchProxy.Create<IProducer<string?, byte[]>, FakeKafkaProducer>();
        return (producer, (FakeKafkaProducer)(object)producer);
    }

    /// <summary>
    /// 构造一份投递报告。
    /// </summary>
    public static DeliveryResult<string?, byte[]> Report(string topic, Message<string?, byte[]> message, PersistenceStatus status)
        => new() { Topic = topic, Message = message, Status = status };

    /// <inheritdoc />
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        args ??= [];
        switch (targetMethod.Name)
        {
            case "ProduceAsync" when args[0] is string topic:
                int sequence;
                lock (Topics)
                {
                    sequence = Topics.Count;
                    Topics.Add(topic);
                }

                try
                {
                    return Task.FromResult(Respond(sequence, topic, (Message<string?, byte[]>)args[1]!));
                }
                catch (Exception exception)
                {
                    return Task.FromException<DeliveryResult<string?, byte[]>>(exception);
                }

            case "Flush":
                return 0;
            case "Dispose":
                return null;
            default:
                throw new NotSupportedException($"The fake producer does not support {targetMethod.Name}.");
        }
    }
}
