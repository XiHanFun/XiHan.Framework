// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Outbox;

/// <summary>
/// 发件箱里的一条消息：与业务数据同事务写入，提交后投递
/// </summary>
/// <remarks>
/// 状态由三个时间决定：<see cref="DispatchedAt"/> 非空为已投递（保留 <see cref="OutboxOptions.DispatchedRetention"/> 后删除），<see cref="DeadLetteredAt"/> 非空为死信（不再投递），都为空时到了 <see cref="NextAttemptAt"/>（为空即已到）才会被取到。索引 <c>(DispatchedAt, DeadLetteredAt, CreatedAt)</c>：投递循环取「未投递、未进死信」的行按创建时间排序，保留期清理按投递时间范围删除，都走它
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
[EntityIndex(nameof(DispatchedAt), nameof(DeadLetteredAt), nameof(CreatedAt))]
public sealed class OutboxMessage : IEntity<Guid>, IHasCreationTime
{
    /// <inheritdoc />
    public Guid Id { get; init; }

    /// <summary>
    /// 主题
    /// </summary>
    /// <remarks>
    /// 主题文法限定最长 128 个字符
    /// </remarks>
    [StringLength(128)]
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// CloudEvents 结构化 JSON
    /// </summary>
    [SuppressMessage("Performance", "CA1819:属性不应返回数组", Justification = "持久化字段，Provider 直接映射为二进制列。")]
    public byte[] Payload { get; set; } = [];

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// 下一次可以投递的时间
    /// </summary>
    /// <remarks>
    /// 写入时等于创建时间；投递失败后按退避推后，到了这个时间才会再被取到。为 <see langword="null"/> 视为已到（列加在已有数据的表上时旧行为空）
    /// </remarks>
    public DateTimeOffset? NextAttemptAt { get; set; }

    /// <summary>
    /// 投递成功时间
    /// </summary>
    /// <remarks>
    /// 未投递为 <see langword="null"/>
    /// </remarks>
    public DateTimeOffset? DispatchedAt { get; set; }

    /// <summary>
    /// 进入死信的时间
    /// </summary>
    /// <remarks>
    /// 尝试次数用尽或载荷无法解码时写入，之后不再投递；行保留供排查，置空并把 <see cref="NextAttemptAt"/> 设为现在即重新投递。未进死信为 <see langword="null"/>
    /// </remarks>
    public DateTimeOffset? DeadLetteredAt { get; set; }

    /// <summary>
    /// 已尝试投递次数
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// 最近一次失败原因
    /// </summary>
    [LongText]
    public string? LastError { get; set; }
}
