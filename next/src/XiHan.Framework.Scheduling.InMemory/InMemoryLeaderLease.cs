// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Leadership;

namespace XiHan.Framework.Scheduling.InMemory;

/// <summary>
/// 进程内租约：续约与释放都回到 <see cref="InMemoryLeaderElection"/> 的表里核对持有者。
/// </summary>
internal sealed class InMemoryLeaderLease : LeaderLease
{
    /// <summary>
    /// 所属的选举。
    /// </summary>
    private readonly InMemoryLeaderElection _election;

    /// <summary>
    /// 创建。
    /// </summary>
    public InMemoryLeaderLease(InMemoryLeaderElection election, JobName resource, string holder)
        : base(resource, holder)
    {
        _election = election;
    }

    /// <inheritdoc />
    public override DateTimeOffset ExpiresAt => _election.ExpiresAt(Resource, Holder);

    /// <inheritdoc />
    public override bool IsHeld => _election.IsHeldBy(Resource, Holder);

    /// <inheritdoc />
    public override ValueTask<Result> RenewAsync(CancellationToken cancellationToken) => ValueTask.FromResult(_election.Renew(Resource, Holder));

    /// <inheritdoc />
    public override ValueTask ReleaseAsync()
    {
        _election.Release(Resource, Holder);
        return ValueTask.CompletedTask;
    }
}
