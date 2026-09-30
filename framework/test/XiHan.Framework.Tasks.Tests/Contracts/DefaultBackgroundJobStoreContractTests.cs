// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.ProviderContractTests;
using XiHan.Framework.ProviderContractTests.Tasks;
using XiHan.Framework.Tasks.BackgroundJobs;
using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;

namespace XiHan.Framework.Tasks.Tests.Contracts;

/// <summary>
/// 默认后台作业存储的提供方契约测试
/// </summary>
public class DefaultBackgroundJobStoreContractTests : BackgroundJobStoreContract
{
    /// <summary>
    /// 创建进程内夹具，声明可控时间
    /// </summary>
    /// <returns>夹具</returns>
    protected override Task<IProviderContractFixture<IBackgroundJobStore>> CreateFixtureAsync()
    {
        var clock = new ManualClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        return Task.FromResult<IProviderContractFixture<IBackgroundJobStore>>(
            new InProcessContractFixture<IBackgroundJobStore>(
                new DefaultBackgroundJobStore(clock),
                ProviderCapabilities.ControllableTime,
                clock));
    }
}
