// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Jobs;

namespace XiHan.Framework.Scheduling.Abstractions.Schedulers;

/// <summary>
/// 作业状态列表的一页
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record JobStatusPage
{
    /// <summary>
    /// 本页的作业状态，按作业名的序数升序
    /// </summary>
    public required IReadOnlyList<JobStatus> Items { get; init; }

    /// <summary>
    /// 下一页的续页令牌
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> 表示已经读完
    /// </remarks>
    public string? ContinuationToken { get; init; }
}
