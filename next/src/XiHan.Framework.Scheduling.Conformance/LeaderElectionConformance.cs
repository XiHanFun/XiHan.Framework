// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Leadership;
using Xunit;

namespace XiHan.Framework.Scheduling.Conformance;

/// <summary>
/// <see cref="ILeaderElection"/> 的一致性套件。继承并实现 <see cref="CreateElection"/>，全部用例随之执行。过期用例用真实时间等待。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class LeaderElectionConformance
{
    /// <summary>
    /// 过期用例在存活时间之外额外等待的时间，给 Provider 的过期粒度留余量。
    /// </summary>
    protected virtual TimeSpan ExpirationGrace => TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// 过期用例使用的存活时间；只有整数秒粒度的 Provider（如 Kubernetes Lease）改为 1 秒。
    /// </summary>
    protected virtual TimeSpan LeaseTimeToLive => TimeSpan.FromMilliseconds(400);

    /// <summary>
    /// 创建被测选举。每个用例调用一次。
    /// </summary>
    protected abstract ILeaderElection CreateElection();

    /// <summary>
    /// 用例 1：空闲资源可获取；被持有时拒绝；释放后可再获取；不同资源互不影响。
    /// </summary>
    [Fact]
    public async Task Acquire_RefusesWhileHeld_AndSucceedsAfterRelease()
    {
        var election = CreateElection();
        var resource = Resource();

        var lease = (await election.TryAcquireAsync(resource, TimeSpan.FromSeconds(30), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(resource, lease.Resource);
        Assert.True(lease.IsHeld);
        Assert.True(lease.ExpiresAt > DateTimeOffset.UtcNow);

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
    /// 用例 2：到期后他人可获取，原持有者续约失败；到期前续约延长到期时刻。
    /// </summary>
    [Fact]
    public async Task Lease_ExpiresUnlessRenewed()
    {
        var election = CreateElection();
        var resource = Resource();
        var ttl = LeaseTimeToLive;

        var lease = (await election.TryAcquireAsync(resource, ttl, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        await Task.Delay(ttl / 2).ConfigureAwait(false);
        var before = lease.ExpiresAt;
        Assert.True((await lease.RenewAsync(CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        Assert.True(lease.ExpiresAt > before);
        Assert.True(lease.IsHeld);

        await Task.Delay(ttl + ExpirationGrace).ConfigureAwait(false);
        Assert.False(lease.IsHeld);
        var successor = (await election.TryAcquireAsync(resource, ttl, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.True(successor.IsHeld);
        Assert.Equal(SchedulingErrors.LeaseHeld, (await lease.RenewAsync(CancellationToken.None).ConfigureAwait(false)).Error?.Code);
        await successor.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 套件内唯一的资源名。
    /// </summary>
    private static JobName Resource() => new("conformance.lease." + Guid.NewGuid().ToString("N")[..8]);
}
