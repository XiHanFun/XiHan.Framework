// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Domain;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions;

/// <summary>
/// 发件箱里的一条消息：与业务数据同事务写入，提交后投递。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class OutboxMessage : IEntity<Guid>, IHasCreationTime
{
    /// <inheritdoc />
    public Guid Id { get; init; }

    /// <summary>
    /// 主题。
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// CloudEvents 结构化 JSON。
    /// </summary>
    [SuppressMessage("Performance", "CA1819:属性不应返回数组", Justification = "持久化字段，Provider 直接映射为二进制列。")]
    public byte[] Payload { get; set; } = [];

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// 投递成功时间；未投递为 <see langword="null"/>。
    /// </summary>
    public DateTimeOffset? DispatchedAt { get; set; }

    /// <summary>
    /// 已尝试投递次数。
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// 最近一次失败原因。
    /// </summary>
    public string? LastError { get; set; }
}
