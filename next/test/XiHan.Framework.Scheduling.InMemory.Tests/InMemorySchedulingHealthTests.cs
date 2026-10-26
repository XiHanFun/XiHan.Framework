// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Scheduling.InMemory.Tests;

/// <summary>
/// 健康项 <c>scheduling.inmemory.leader</c>：引擎在跑时 Ready（进程内租约持有与否都不影响），应用停止后 Stopped。
/// </summary>
public sealed class InMemorySchedulingHealthTests
{
    [Fact]
    public async Task Health_IsReadyWhileRunning_AndStoppedAfterShutdown()
    {
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<InMemorySchedulingFeature>(), [new("XiHan:Scheduling:PollIntervalSeconds", "3600")]);

        var running = Assert.Single(host.App.Health().Contributions, static c => c.Name == InMemorySchedulingFeature.HealthName);
        Assert.Equal(LifecycleState.Ready, running.State);
        Assert.Null(running.Error);

        await host.App.StopAsync(CancellationToken.None);

        var stopped = Assert.Single(host.App.Health().Contributions, static c => c.Name == InMemorySchedulingFeature.HealthName);
        Assert.Equal(LifecycleState.Stopped, stopped.State);
    }
}
