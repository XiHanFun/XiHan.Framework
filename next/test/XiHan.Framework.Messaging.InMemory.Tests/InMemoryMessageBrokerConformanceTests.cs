// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Conformance;

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
}
