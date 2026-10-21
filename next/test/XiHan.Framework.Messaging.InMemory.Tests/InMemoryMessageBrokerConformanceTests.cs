// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Conformance;

namespace XiHan.Framework.Messaging.InMemory.Tests;

public sealed class InMemoryMessageBrokerConformanceTests : MessageBrokerConformance
{
    protected override IFeature CreateFeature() => new InMemoryMessagingFeature();
}
