// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;
using RabbitMQ.Client;

namespace XiHan.Framework.Messaging.RabbitMQ.Tests;

/// <summary>
/// 打桩的 <see cref="IConnection"/>。
/// </summary>
/// <remarks>
/// 以 <see cref="DispatchProxy"/> 生成：每次创建通道得到一个新的 <see cref="FakeRabbitChannel"/> 并记下创建选项；第一个通道是代理的发布通道，之后每个订阅主题一个。
/// </remarks>
public class FakeRabbitConnection : DispatchProxy
{
    /// <summary>
    /// 创建过的通道，按顺序。
    /// </summary>
    public List<FakeRabbitChannel> Channels { get; } = [];

    /// <summary>
    /// 新通道创建后的配置（如设置确认结果）。
    /// </summary>
    public Action<FakeRabbitChannel>? OnChannel { get; set; }

    /// <summary>
    /// 创建一个打桩连接。
    /// </summary>
    public static (IConnection Connection, FakeRabbitConnection Fake) Create()
    {
        var connection = DispatchProxy.Create<IConnection, FakeRabbitConnection>();
        return (connection, (FakeRabbitConnection)(object)connection);
    }

    /// <inheritdoc />
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        args ??= [];
        switch (targetMethod.Name)
        {
            case "CreateChannelAsync":
                var channel = DispatchProxy.Create<IChannel, FakeRabbitChannel>();
                var fake = (FakeRabbitChannel)(object)channel;
                fake.Options = args.OfType<CreateChannelOptions>().SingleOrDefault();
                lock (Channels)
                {
                    fake.Number = Channels.Count;
                    Channels.Add(fake);
                }

                OnChannel?.Invoke(fake);
                return Task.FromResult(channel);
            case "get_IsOpen":
                return true;
            case "get_CloseReason":
                return null;
            case "DisposeAsync":
                return ValueTask.CompletedTask;
            case "Dispose":
                return null;
            default:
                throw new NotSupportedException($"The fake connection does not support {targetMethod.Name}.");
        }
    }
}
