// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Database.Entities;

/// <summary>
/// 作业行。
/// </summary>
/// <remarks>
/// <c>Id</c> 是作业名，<c>JobRecord</c> 的其余字段逐列存放，数据是 JSON 文本，来源存枚举的整数值。并发戳让独占运行对 <see cref="ActiveRunId"/> 的占用与释放在跨进程下是原子的比较并交换。不实现 <c>IMultiTenant</c>，存储不按租户过滤。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class JobEntity : IEntity<string>, IHasConcurrencyStamp
{
    /// <inheritdoc />
    [StringLength(128)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 作业类型键。
    /// </summary>
    [StringLength(512)]
    public string JobTypeName { get; set; } = string.Empty;

    /// <summary>
    /// 运行时传给作业的数据，JSON 对象文本。
    /// </summary>
    /// <remarks>
    /// 长文本列。
    /// </remarks>
    [LongText]
    public string Data { get; set; } = "{}";

    /// <summary>
    /// 允许多次运行重叠。
    /// </summary>
    public bool AllowConcurrent { get; set; }

    /// <summary>
    /// 每个实例各自运行。
    /// </summary>
    public bool RunOnEveryInstance { get; set; }

    /// <summary>
    /// 节点失联时重新入队。
    /// </summary>
    public bool RequestsRecovery { get; set; }

    /// <summary>
    /// 说明。
    /// </summary>
    [StringLength(1024)]
    public string? Description { get; set; }

    /// <summary>
    /// 是否暂停。
    /// </summary>
    public bool Paused { get; set; }

    /// <summary>
    /// 不允许并发时正在运行的运行 ID。
    /// </summary>
    /// <remarks>
    /// 没有为空。
    /// </remarks>
    [StringLength(128)]
    public string? ActiveRunId { get; set; }

    /// <summary>
    /// 来源（<c>JobSource</c> 的整数值）。
    /// </summary>
    public int Source { get; set; }

    /// <summary>
    /// 最近写入时刻。
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// 代码定义的指纹。
    /// </summary>
    /// <remarks>
    /// SHA-256 十六进制；运行时登记的作业为 <see langword="null"/>（ADR-0053）。
    /// </remarks>
    [StringLength(64)]
    public string? CodeFingerprint { get; set; }

    /// <summary>
    /// 重试次数。
    /// </summary>
    /// <remarks>
    /// 与等待时间同时为空表示不重试（ADR-0060）。
    /// </remarks>
    public int? RetryMaxRetries { get; set; }

    /// <summary>
    /// 第一次重试前的等待（毫秒）。
    /// </summary>
    public long? RetryDelayMilliseconds { get; set; }

    /// <summary>
    /// 等待方式（<c>JobRetryBackoff</c> 的整数值）。
    /// </summary>
    public int? RetryBackoff { get; set; }

    /// <summary>
    /// 指数退避的等待上限（毫秒）。
    /// </summary>
    public long? RetryMaxDelayMilliseconds { get; set; }

    /// <summary>
    /// 单次执行时限（毫秒）。
    /// </summary>
    /// <remarks>
    /// 空为不限。
    /// </remarks>
    public long? TimeoutMilliseconds { get; set; }

    /// <inheritdoc />
    [StringLength(32)]
    public string ConcurrencyStamp { get; set; } = string.Empty;
}
