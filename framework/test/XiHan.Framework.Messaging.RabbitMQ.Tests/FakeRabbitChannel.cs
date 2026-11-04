// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;
using RabbitMQ.Client;

namespace XiHan.Framework.Messaging.RabbitMQ.Tests;

/// <summary>
/// 打桩的 <see cref="IChannel"/>
/// </summary>
/// <remarks>
/// 以 <see cref="DispatchProxy"/> 生成，只实现代理用到的成员：记录交换机与队列的声明、绑定、发布、确认与退回，按 <see cref="Confirm"/> 决定每次发布的确认结果，<see cref="HoldConfirms"/> 时确认挂起到 <see cref="ReleaseConfirms"/>。其余成员抛 <see cref="NotSupportedException"/>
/// </remarks>
public class FakeRabbitChannel : DispatchProxy
{
    /// <summary>
    /// 放行挂起确认的信号
    /// </summary>
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 本通道的序号，从 0 开始，按连接创建通道的顺序
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// 创建通道时的选项
    /// </summary>
    public CreateChannelOptions? Options { get; set; }

    /// <summary>
    /// 声明过的队列，按顺序
    /// </summary>
    public List<QueueDeclaration> Declared { get; } = [];

    /// <summary>
    /// 声明过的交换机名与类型，按顺序
    /// </summary>
    public List<(string Name, string Type, bool Durable)> Exchanges { get; } = [];

    /// <summary>
    /// 绑定过的队列、交换机与路由键，按顺序
    /// </summary>
    public List<(string Queue, string Exchange, string RoutingKey)> Bindings { get; } = [];

    /// <summary>
    /// 发布记录，按顺序
    /// </summary>
    public List<PublishedMessage> Published { get; } = [];

    /// <summary>
    /// 确认（ack）过的投递标签
    /// </summary>
    public List<ulong> Acked { get; } = [];

    /// <summary>
    /// 退回（nack）过的投递标签与是否重新入队
    /// </summary>
    public List<(ulong DeliveryTag, bool Requeue)> Nacked { get; } = [];

    /// <summary>
    /// 消费时登记的消费者
    /// </summary>
    public IAsyncBasicConsumer? Consumer { get; private set; }

    /// <summary>
    /// 每次发布的确认结果
    /// </summary>
    /// <remarks>
    /// 参数是发布序号（从 0 开始）与路由键；返回空为确认，否则发布以返回的异常结束
    /// </remarks>
    public Func<int, string, Exception?> Confirm { get; set; } = static (_, _) => null;

    /// <summary>
    /// 是否挂起确认
    /// </summary>
    /// <remarks>
    /// 为真时发布写出后不完成，直到 <see cref="ReleaseConfirms"/>
    /// </remarks>
    public bool HoldConfirms { get; set; }

    /// <summary>
    /// 放行全部挂起的确认
    /// </summary>
    public void ReleaseConfirms() => _release.TrySetResult();

    /// <inheritdoc />
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        args ??= [];
        switch (targetMethod.Name)
        {
            case "get_IsOpen":
                return true;
            case "get_IsClosed":
                return false;
            case "get_CloseReason":
                return null;
            case "get_ChannelNumber":
                return Number + 1;
            case "QueueDeclareAsync":
                var queue = (string)args[0]!;
                lock (Declared)
                {
                    Declared.Add(new QueueDeclaration(queue, (bool)args[1]!, (bool)args[2]!, (bool)args[3]!));
                }

                return Task.FromResult(new QueueDeclareOk(queue, 0, 0));
            case "ExchangeDeclareAsync":
                lock (Exchanges)
                {
                    Exchanges.Add(((string)args[0]!, (string)args[1]!, (bool)args[2]!));
                }

                return Task.CompletedTask;
            case "QueueBindAsync":
                lock (Bindings)
                {
                    Bindings.Add(((string)args[0]!, (string)args[1]!, (string)args[2]!));
                }

                return Task.CompletedTask;
            case "BasicPublishAsync":
                return new ValueTask(Publish((string)args[0]!, (string)args[1]!, (bool)args[2]!, (IReadOnlyBasicProperties)args[3]!, (ReadOnlyMemory<byte>)args[4]!));
            case "BasicQosAsync":
            case "BasicCancelAsync":
            case "CloseAsync":
                return Task.CompletedTask;
            case "BasicConsumeAsync":
                Consumer = args.OfType<IAsyncBasicConsumer>().Single();
                return Task.FromResult("consumer-" + Number);
            case "BasicAckAsync":
                lock (Acked)
                {
                    Acked.Add((ulong)args[0]!);
                }

                return ValueTask.CompletedTask;
            case "BasicNackAsync":
                lock (Nacked)
                {
                    Nacked.Add(((ulong)args[0]!, (bool)args[2]!));
                }

                return ValueTask.CompletedTask;
            case "DisposeAsync":
                return ValueTask.CompletedTask;
            case "Dispose":
                return null;
            default:
                throw new NotSupportedException($"The fake channel does not support {targetMethod.Name}.");
        }
    }

    /// <summary>
    /// 记录一次发布，按 <see cref="Confirm"/> 给出确认结果
    /// </summary>
    private Task Publish(string exchange, string routingKey, bool mandatory, IReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body)
    {
        int sequence;
        lock (Published)
        {
            sequence = Published.Count;
            Published.Add(new PublishedMessage(exchange, routingKey, mandatory, properties, body.ToArray()));
        }

        var outcome = Confirm(sequence, routingKey);
        return HoldConfirms ? AwaitConfirmAsync(outcome) : outcome is null ? Task.CompletedTask : Task.FromException(outcome);
    }

    /// <summary>
    /// 等放行后按结果完成
    /// </summary>
    private async Task AwaitConfirmAsync(Exception? outcome)
    {
        await _release.Task.ConfigureAwait(false);
        if (outcome is not null)
        {
            throw outcome;
        }
    }
}
