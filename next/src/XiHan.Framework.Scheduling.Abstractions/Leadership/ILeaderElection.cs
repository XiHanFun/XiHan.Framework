// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Jobs;

namespace XiHan.Framework.Scheduling.Abstractions.Leadership;

/// <summary>
/// 租约式领导选举（RFC-0013）。
/// </summary>
/// <remarks>
/// 空闲（或过期）的资源授予租约，被持有时返回 <see cref="SchedulingErrors.LeaseHeld"/>。不可重入。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface ILeaderElection
{
    /// <summary>
    /// 尝试获取资源的领导租约，存活 <paramref name="timeToLive"/>。
    /// </summary>
    ValueTask<Result<LeaderLease>> TryAcquireAsync(JobName resource, TimeSpan timeToLive, CancellationToken cancellationToken);
}
