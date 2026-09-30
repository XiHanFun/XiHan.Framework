// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.EventBus.Abstractions.Distributed;

/// <summary>
/// 发件箱待送事件计数器
/// </summary>
/// <remarks>
/// 供应用在删除租户前确认该租户的发件箱已排空。
/// </remarks>
public interface IOutboxPendingEventCounter
{
    /// <summary>
    /// 统计指定投递目标在全部按租户定位存储的发件箱（实现 <see cref="ITenantScopedEventOutbox"/>）中尚未删除的事件数
    /// </summary>
    /// <param name="target">投递目标</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>尚未删除的事件数</returns>
    /// <exception cref="NotSupportedException">没有任何已配置的发件箱实现 <see cref="ITenantScopedEventOutbox"/></exception>
    Task<long> GetPendingCountAsync(OutboxDeliveryTarget target, CancellationToken cancellationToken = default);
}
