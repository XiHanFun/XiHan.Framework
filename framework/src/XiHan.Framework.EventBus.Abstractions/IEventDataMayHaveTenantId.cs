// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.EventBus.Abstractions;

/// <summary>
/// 事件数据对象（或事件传输对象）可以实现此接口
/// 来表示该事件可能与租户相关
/// 如果事件数据类始终与租户相关，则直接实现
/// <see cref="IsMultiTenant"/> 接口而不是此接口
/// 此接口通常由泛型事件处理程序实现，其中泛型
/// 参数可能实现 <see cref="IsMultiTenant"/> 或不实现
/// </summary>
public interface IEventDataMayHaveTenantId
{
    /// <summary>
    /// 如果此事件数据具有租户唯一标识信息，则返回 true
    /// 如果是这样，它应该设置 <paramref name="tenantId"/> 参数
    /// 否则，<paramref name="tenantId"/> 参数值不应该是有意义的
    /// （它将如预期那样为 null，但不表示具有 null 租户唯一标识的租户）
    /// </summary>
    /// <param name="tenantId">
    /// 如果此方法返回 true，则设置的租户唯一标识
    /// </param>
    bool IsMultiTenant(out long? tenantId);
}
