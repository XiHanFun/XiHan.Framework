// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Database.Entities;

/// <summary>
/// 租约行。
/// </summary>
/// <remarks>
/// 实例锁（<c>Id</c> 是实例 ID）与轮询领导权（<c>Id</c> 是资源名）共用。并发戳让“读当前行再比对后更新”在跨进程下是原子的比较并交换：新增靠主键唯一约束，接管与续约靠 <see cref="IHasConcurrencyStamp"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WorkflowLockRecord : IEntity<string>, IHasConcurrencyStamp
{
    /// <inheritdoc />
    [StringLength(128)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 持有者：节点的持有者标识加本次获取的随机后缀，释放与续约只对自己的行生效。
    /// </summary>
    [StringLength(256)]
    public string Holder { get; set; } = string.Empty;

    /// <summary>
    /// 租约到期时间。
    /// </summary>
    /// <remarks>
    /// 过期的行可被别的持有者接管。
    /// </remarks>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <inheritdoc />
    [StringLength(32)]
    public string ConcurrencyStamp { get; set; } = string.Empty;
}
