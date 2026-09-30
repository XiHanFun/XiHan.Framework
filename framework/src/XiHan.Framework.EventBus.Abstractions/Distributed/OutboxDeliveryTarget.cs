// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.EventBus.Abstractions.Distributed;

/// <summary>
/// 发件箱投递目标，对应一个需要单独扫描发件箱的租户
/// </summary>
public sealed class OutboxDeliveryTarget
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="tenantId">租户标识，必须大于零</param>
    /// <param name="tenantName">租户名称</param>
    /// <param name="isEnabled">是否启用；停用的目标仍会被扫描直至排空，但拒绝新事件入箱</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tenantId"/> 小于或等于零</exception>
    public OutboxDeliveryTarget(long tenantId, string? tenantName = null, bool isEnabled = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tenantId);

        TenantId = tenantId;
        TenantName = tenantName;
        IsEnabled = isEnabled;
    }

    /// <summary>
    /// 租户标识
    /// </summary>
    public long TenantId { get; }

    /// <summary>
    /// 租户名称
    /// </summary>
    public string? TenantName { get; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; }
}
