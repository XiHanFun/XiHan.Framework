// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Conformance;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Messaging.InMemory.Tests;

/// <summary>
/// 内存代理跑一致性套件
/// </summary>
/// <remarks>
/// 内存代理每个宿主各有一个进程内代理，两个宿主不共用，多实例用例跳过
/// </remarks>
public sealed class InMemoryMessageBrokerConformanceTests : MessageBrokerConformance
{
    /// <inheritdoc />
    protected override bool SupportsMultipleInstances => false;

    /// <inheritdoc />
    protected override IFeature CreateFeature() => new InMemoryMessagingFeature();

    /// <inheritdoc />
    /// <remarks>
    /// 读宿主里那个代理的死信列表
    /// </remarks>
    protected override ValueTask<IReadOnlyList<MessageEnvelope>> ReadDeadLettersAsync(TestHost host, string topic, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        var broker = host.Services.GetRequiredService<InMemoryMessageBroker>();
        return ValueTask.FromResult<IReadOnlyList<MessageEnvelope>>([.. broker.DeadLetters(topic).Select(static delivery => delivery.Envelope)]);
    }
}
