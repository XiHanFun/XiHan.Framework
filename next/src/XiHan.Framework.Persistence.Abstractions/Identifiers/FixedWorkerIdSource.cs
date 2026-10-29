// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Identifiers;

/// <summary>
/// 由配置显式指定的机器位（<c>XiHan:Persistence:Snowflake:WorkerId</c>）。
/// </summary>
/// <remarks>
/// 运维自己保证集群内不重复。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class FixedWorkerIdSource : IWorkerIdSource
{
    /// <summary>
    /// 创建。
    /// </summary>
    /// <remarks>
    /// 越界抛出 <see cref="ArgumentOutOfRangeException"/>。
    /// </remarks>
    /// <param name="workerId">机器位（0–1023）。</param>
    public FixedWorkerIdSource(int workerId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(workerId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(workerId, SnowflakeId.MaxWorkerId);
        WorkerId = workerId;
    }

    /// <summary>
    /// 机器位。
    /// </summary>
    public int WorkerId { get; }

    /// <inheritdoc />
    public Maybe<int> Current => WorkerId;
}
