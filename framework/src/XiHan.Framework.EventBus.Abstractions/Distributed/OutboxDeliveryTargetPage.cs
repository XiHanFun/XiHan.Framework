// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.EventBus.Abstractions.Distributed;

/// <summary>
/// 发件箱投递目标目录的一页
/// </summary>
public sealed class OutboxDeliveryTargetPage
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="targets">本页的投递目标</param>
    /// <param name="nextCursor">下一页游标；为 null 或空字符串表示已到目录末尾</param>
    /// <exception cref="ArgumentNullException"><paramref name="targets"/> 为 null</exception>
    public OutboxDeliveryTargetPage(IReadOnlyList<OutboxDeliveryTarget> targets, string? nextCursor)
    {
        ArgumentNullException.ThrowIfNull(targets);

        Targets = targets;
        NextCursor = string.IsNullOrEmpty(nextCursor) ? null : nextCursor;
    }

    /// <summary>
    /// 没有任何目标且位于目录末尾的空页
    /// </summary>
    public static OutboxDeliveryTargetPage Empty { get; } = new([], null);

    /// <summary>
    /// 本页的投递目标
    /// </summary>
    public IReadOnlyList<OutboxDeliveryTarget> Targets { get; }

    /// <summary>
    /// 下一页游标，为 null 表示已到目录末尾
    /// </summary>
    public string? NextCursor { get; }
}
