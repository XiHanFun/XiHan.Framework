// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Scheduling.Conformance;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Scheduling.InMemory.Tests;

/// <summary>
/// 让 <see cref="InMemoryJobScheduler"/> 跑完整套 <see cref="JobSchedulerConformance"/> 用例；轮询周期缩短到 50 毫秒。
/// </summary>
public sealed class InMemoryJobSchedulerConformanceTests : JobSchedulerConformance
{
    /// <inheritdoc />
    protected override async Task<JobSchedulerHost> StartAsync(Action<IServiceCollection> configure)
    {
        var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemorySchedulingFeature>(),
            [new("XiHan:Scheduling:InMemory:PollIntervalSeconds", "0.05")],
            configure);
        return new JobSchedulerHost(host.Services.GetRequiredService<IJobScheduler>(), host);
    }
}
