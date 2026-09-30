// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Distributed;
using XiHan.Framework.ProviderContractTests;
using XiHan.Framework.ProviderContractTests.EventBus;

namespace XiHan.Framework.EventBus.Tests.Contracts;

/// <summary>
/// 默认收件箱的提供方契约测试
/// </summary>
public class DefaultEventInboxContractTests : InboxContract
{
    /// <summary>
    /// 创建进程内夹具，不声明附加能力
    /// </summary>
    /// <returns>夹具</returns>
    protected override Task<IProviderContractFixture<IEventInbox>> CreateFixtureAsync()
    {
        return Task.FromResult<IProviderContractFixture<IEventInbox>>(
            new InProcessContractFixture<IEventInbox>(new DefaultEventInbox(), ProviderCapabilities.None));
    }
}
