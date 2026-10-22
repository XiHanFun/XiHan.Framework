// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Conformance;

namespace XiHan.Framework.Messaging.RabbitMQ.Tests;

/// <summary>
/// 真实 RabbitMQ 来自环境变量 <c>XIHAN_TEST_RABBITMQ</c>（如 <c>amqp://guest:guest@localhost:5672</c>）；未设置时跳过需要它的用例。
/// </summary>
public sealed class RabbitMQMessageBrokerConformanceTests : MessageBrokerConformance
{
    /// <summary>
    /// 提供 RabbitMQ 连接 URI 的环境变量名。
    /// </summary>
    public const string EnvironmentVariable = "XIHAN_TEST_RABBITMQ";

    /// <summary>
    /// 本次运行独有的队列前缀，避免与其他运行的队列互相干扰。
    /// </summary>
    private readonly string _prefix = $"xihan-test.{Guid.NewGuid():N}.";

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, string?>> Configuration =>
    [
        new("XiHan:Messaging:RabbitMQ:Configuration", Environment.GetEnvironmentVariable(EnvironmentVariable)),
        new("XiHan:Messaging:RabbitMQ:QueuePrefix", _prefix),
    ];

    /// <inheritdoc />
    protected override IFeature CreateFeature()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariable)), $"Set the environment variable {EnvironmentVariable} to a RabbitMQ URI to run this test.");
        return new RabbitMQMessagingFeature();
    }
}
