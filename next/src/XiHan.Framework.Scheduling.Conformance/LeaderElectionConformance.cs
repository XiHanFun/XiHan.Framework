// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Leadership;
using Xunit;

namespace XiHan.Framework.Scheduling.Conformance;

/// <summary>
/// <see cref="ILeaderElection"/> 的一致性套件。
/// </summary>
/// <remarks>
/// 继承并实现 <see cref="CreateElection"/>，全部用例随之执行。到期按套件传入的时钟判定，用例推进时钟而不是等待真实时间，结果与机器快慢无关。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class LeaderElectionConformance
{
    /// <summary>
    /// 过期用例使用的存活时间。
    /// </summary>
    /// <remarks>
    /// 只有整数秒粒度的 Provider（如 Kubernetes Lease）改为 1 秒。
    /// </remarks>
    protected virtual TimeSpan LeaseTimeToLive => TimeSpan.FromMilliseconds(400);

    /// <summary>
    /// 创建被测选举。
    /// </summary>
    /// <remarks>
    /// 每个用例调用一次；选举与它产生的租约都按 <paramref name="clock"/> 判定到期。
    /// </remarks>
    protected abstract ILeaderElection CreateElection(TimeProvider clock);

    /// <summary>
    /// 用例 1：空闲资源可获取。
    /// </summary>
    /// <remarks>
    /// 被持有时拒绝；释放后可再获取；不同资源互不影响。
    /// </remarks>
    [Fact]
    public async Task Acquire_RefusesWhileHeld_AndSucceedsAfterRelease()
    {
        var clock = new ManualClock(Start);
        var election = CreateElection(clock);
        var resource = Resource();

        var lease = (await election.TryAcquireAsync(resource, TimeSpan.FromSeconds(30), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(resource, lease.Resource);
        Assert.True(lease.IsHeld);
        Assert.True(lease.ExpiresAt > clock.GetUtcNow());

        var refused = await election.TryAcquireAsync(resource, TimeSpan.FromSeconds(30), CancellationToken.None).ConfigureAwait(false);
        Assert.Equal(SchedulingErrors.LeaseHeld, refused.Error?.Code);
        Assert.True((await election.TryAcquireAsync(Resource(), TimeSpan.FromSeconds(30), CancellationToken.None).ConfigureAwait(false)).IsSuccess);

        await lease.ReleaseAsync().ConfigureAwait(false);
        Assert.False(lease.IsHeld);
        await lease.ReleaseAsync().ConfigureAwait(false);
        // 持有者标识是实例身份（如 Pod 名），同一实例再次获取可以相同；旧租约对象必须已失效
        var again = (await election.TryAcquireAsync(resource, TimeSpan.FromSeconds(30), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.True(again.IsHeld);
        Assert.Equal(SchedulingErrors.LeaseHeld, (await lease.RenewAsync(CancellationToken.None).ConfigureAwait(false)).Error?.Code);
    }

    /// <summary>
    /// 用例 2：到期后他人可获取，原持有者续约失败。
    /// </summary>
    /// <remarks>
    /// 到期前续约延长到期时刻。
    /// </remarks>
    [Fact]
    public async Task Lease_ExpiresUnlessRenewed()
    {
        var clock = new ManualClock(Start);
        var election = CreateElection(clock);
        var resource = Resource();
        var ttl = LeaseTimeToLive;

        var lease = (await election.TryAcquireAsync(resource, ttl, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        clock.Advance(ttl / 2);
        var before = lease.ExpiresAt;
        Assert.True((await lease.RenewAsync(CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        Assert.True(lease.ExpiresAt > before);
        Assert.True(lease.IsHeld);

        // 越过续约后的到期时刻：本地不再持有，他人可获取，原持有者续约失败
        clock.Advance(ttl + TimeSpan.FromMilliseconds(1));
        Assert.False(lease.IsHeld);
        var successor = (await election.TryAcquireAsync(resource, ttl, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.True(successor.IsHeld);
        Assert.Equal(SchedulingErrors.LeaseHeld, (await lease.RenewAsync(CancellationToken.None).ConfigureAwait(false)).Error?.Code);
        await successor.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 用例时钟的起点。
    /// </summary>
    /// <remarks>
    /// 整秒，避开只有微秒精度的存储（如 Kubernetes MicroTime）的截断。
    /// </remarks>
    private static DateTimeOffset Start { get; } = new(2026, 10, 31, 8, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 套件内唯一的资源名。
    /// </summary>
    private static JobName Resource() => new("conformance.lease." + Guid.NewGuid().ToString("N")[..8]);
}
