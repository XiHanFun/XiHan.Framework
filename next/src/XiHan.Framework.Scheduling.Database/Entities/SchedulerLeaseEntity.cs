// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Database.Entities;

/// <summary>
/// 领导租约行：<c>Id</c> 是资源名，<see cref="Holder"/> 是持有节点，<see cref="Token"/> 是本次获取的随机令牌——续约与释放只对令牌相同的行生效，同一节点释放后再获取得到新令牌，旧租约对象随之失效。
/// 新增靠主键唯一约束，接管与续约靠并发戳，跨进程都是原子的比较并交换。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class SchedulerLeaseEntity : IEntity<string>, IHasConcurrencyStamp
{
    /// <inheritdoc />
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 持有节点的 ID。
    /// </summary>
    public string Holder { get; set; } = string.Empty;

    /// <summary>
    /// 本次获取的令牌。
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// 到期时刻；过期的行可被任何节点接管。
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <inheritdoc />
    public string ConcurrencyStamp { get; set; } = string.Empty;
}
