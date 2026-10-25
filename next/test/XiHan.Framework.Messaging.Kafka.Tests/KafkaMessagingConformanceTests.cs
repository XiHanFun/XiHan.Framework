// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Conformance;

namespace XiHan.Framework.Messaging.Kafka.Tests;

/// <summary>
/// 让 <see cref="KafkaMessagingFeature"/> 对真实 Kafka 跑完整套队列与流用例；未设置 <c>XIHAN_TEST_KAFKA</c> 时跳过。每个用例用独立的主题前缀与消费组，互不干扰。
/// </summary>
public sealed class KafkaMessagingConformanceTests : MessageStreamConformance
{
    /// <summary>
    /// 指向测试 Kafka 引导地址的环境变量名。
    /// </summary>
    public const string EnvironmentVariable = "XIHAN_TEST_KAFKA";

    /// <summary>
    /// 本用例的主题前缀与消费组。
    /// </summary>
    private readonly string _suffix = Guid.NewGuid().ToString("N")[..12];

    /// <inheritdoc />
    protected override TimeSpan DeliveryTimeout => TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, string?>> Configuration =>
    [
        new("XiHan:Messaging:Kafka:BootstrapServers", Environment.GetEnvironmentVariable(EnvironmentVariable)),
        new("XiHan:Messaging:Kafka:TopicPrefix", $"xihan-test.{_suffix}."),
        new("XiHan:Messaging:Kafka:GroupId", $"xihan-test-{_suffix}"),
    ];

    /// <inheritdoc />
    protected override IFeature CreateFeature()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariable)), $"Set the environment variable {EnvironmentVariable} to Kafka bootstrap servers to run this test.");
        return new KafkaMessagingFeature();
    }
}
