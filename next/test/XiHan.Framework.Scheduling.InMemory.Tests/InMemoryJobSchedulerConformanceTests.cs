// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Scheduling.Conformance;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Scheduling.InMemory.Tests;

/// <summary>
/// 内存调度特性通过调度器套件。
/// </summary>
/// <remarks>
/// 轮询周期设得很长，后台循环不动作；用例推进套件的时钟、主动轮询。维护间隔按套件时钟 0.2 秒，推进时钟的用例里会穿插执行。
/// </remarks>
public sealed class InMemoryJobSchedulerConformanceTests : JobSchedulerConformance
{
    /// <inheritdoc />
    protected override async Task<JobSchedulerHost> StartAsync(Action<IServiceCollection> configure, IEnumerable<KeyValuePair<string, string?>>? configuration = null)
    {
        var settings = new List<KeyValuePair<string, string?>> { new("XiHan:Scheduling:PollIntervalSeconds", "3600"), new("XiHan:Scheduling:HousekeepingIntervalSeconds", "0.2") };
        if (configuration is not null)
        {
            settings.AddRange(configuration);
        }

        var host = await TestHost.StartAsync(static b => b.UseFeature<InMemorySchedulingFeature>(), settings, configure);
        var engine = host.Services.GetRequiredService<JobSchedulerEngine>();
        return new JobSchedulerHost(engine, engine.PollOnceAsync, host);
    }
}
