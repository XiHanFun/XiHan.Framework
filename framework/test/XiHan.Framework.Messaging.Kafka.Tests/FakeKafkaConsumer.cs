// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;
using Confluent.Kafka;

namespace XiHan.Framework.Messaging.Kafka.Tests;

/// <summary>
/// 打桩的消费者：一个分区、预置的消息、可回退的读位置
/// </summary>
/// <remarks>
/// 以 <see cref="DispatchProxy"/> 生成，只实现代理用到的成员：<c>Consume(CancellationToken)</c> 按读位置返回消息，读完后阻塞到有新位置或取消；<c>Seek</c> 改读位置；<c>Commit(ConsumeResult)</c> 记下位点。<see cref="ConsumeFailures"/> 里的异常在下一次取消息时依次抛出。其余成员抛 <see cref="NotSupportedException"/>
/// </remarks>
public class FakeKafkaConsumer : DispatchProxy
{
    /// <summary>
    /// 有消息可读的信号
    /// </summary>
    /// <remarks>
    /// 读完时换成一个未完成的新信号，回退时完成它
    /// </remarks>
    private TaskCompletionSource _available = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 保护读位置与记录的锁
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 预置的消息值，下标即位点
    /// </summary>
    private readonly List<byte[]> _values = [];

    /// <summary>
    /// 下一次读的位点
    /// </summary>
    private int _position;

    /// <summary>
    /// 订阅的主题
    /// </summary>
    public string? Topic { get; private set; }

    /// <summary>
    /// 提交过的位点，按顺序
    /// </summary>
    public List<long> Committed { get; } = [];

    /// <summary>
    /// 回退过的位点，按顺序
    /// </summary>
    public List<long> Seeks { get; } = [];

    /// <summary>
    /// 下一次取消息时依次抛出的异常
    /// </summary>
    public Queue<Exception> ConsumeFailures { get; } = new();

    /// <summary>
    /// 是否已关闭
    /// </summary>
    public bool Closed { get; private set; }

    /// <summary>
    /// 创建一个预置了消息值的打桩消费者
    /// </summary>
    public static (IConsumer<string?, byte[]> Consumer, FakeKafkaConsumer Fake) Create(params byte[][] values)
    {
        var consumer = DispatchProxy.Create<IConsumer<string?, byte[]>, FakeKafkaConsumer>();
        var fake = (FakeKafkaConsumer)(object)consumer;
        fake._values.AddRange(values);
        return (consumer, fake);
    }

    /// <summary>
    /// 在锁内读一份记录的快照
    /// </summary>
    public T Read<T>(Func<FakeKafkaConsumer, T> read)
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
        switch (targetMethod.Name)
        {
            case "Subscribe" when args[0] is string topic:
                Topic = topic;
                return null;
            case "Consume" when args[0] is CancellationToken cancellationToken:
                return Consume(cancellationToken);
            case "Seek" when args[0] is TopicPartitionOffset offset:
                lock (_gate)
                {
                    Seeks.Add(offset.Offset.Value);
                    _position = (int)offset.Offset.Value;
                    _available.TrySetResult();
                }

                return null;
            case "Commit" when args[0] is ConsumeResult<string?, byte[]> result:
                lock (_gate)
                {
                    Committed.Add(result.Offset.Value);
                }

                return null;
            case "Close":
                Closed = true;
                return null;
            case "Dispose":
                return null;
            default:
                throw new NotSupportedException($"The fake consumer does not support {targetMethod.Name}.");
        }
    }

    /// <summary>
    /// 按读位置返回下一条消息，读完后阻塞到有新位置或取消
    /// </summary>
    private ConsumeResult<string?, byte[]> Consume(CancellationToken cancellationToken)
    {
        while (true)
        {
            Task available;
            lock (_gate)
            {
                if (ConsumeFailures.TryDequeue(out var failure))
                {
                    throw failure;
                }

                if (_position < _values.Count)
                {
                    var offset = _position++;
                    return new ConsumeResult<string?, byte[]>
                    {
                        TopicPartitionOffset = new TopicPartitionOffset(Topic ?? "unsubscribed", new Partition(0), new Offset(offset)),
                        Message = new Message<string?, byte[]> { Value = _values[offset] },
                    };
                }

                if (_available.Task.IsCompleted)
                {
                    _available = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                }

                available = _available.Task;
            }

            // 读完了：等回退或取消，取消时抛 OperationCanceledException，与真实消费者一致
            available.Wait(cancellationToken);
        }
    }
}
