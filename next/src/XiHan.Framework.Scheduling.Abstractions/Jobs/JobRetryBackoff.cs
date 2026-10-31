// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Jobs;

/// <summary>
/// 重试之间的等待方式。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public enum JobRetryBackoff
{
    /// <summary>
    /// 每次都等 <see cref="JobRetryPolicy.Delay"/>。
    /// </summary>
    Fixed = 0,

    /// <summary>
    /// 第 n 次重试等 <see cref="JobRetryPolicy.Delay"/> × 2^(n-1)。
    /// </summary>
    /// <remarks>
    /// 不超过 <see cref="JobRetryPolicy.MaxDelay"/>。
    /// </remarks>
    Exponential = 1,
}
