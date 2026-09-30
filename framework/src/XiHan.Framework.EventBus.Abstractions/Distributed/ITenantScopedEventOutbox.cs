// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.EventBus.Abstractions.Distributed;

/// <summary>
/// 按当前租户上下文定位存储的发件箱
/// </summary>
/// <remarks>
/// 领取与计数作用于当前租户上下文所在的存储；删除可按实现记录的领取来源定位。
/// 发送循环只对实现了本接口的发件箱按投递目标切换租户上下文扫描。
/// </remarks>
public interface ITenantScopedEventOutbox : IEventOutbox
{
    /// <summary>
    /// 统计当前租户存储中尚未删除的事件数
    /// </summary>
    /// <remarks>
    /// 待发送与已领取但尚未删除的事件都计入。任一存储不可达时抛出异常，不返回部分结果。
    /// </remarks>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>尚未删除的事件数</returns>
    Task<long> GetPendingCountAsync(CancellationToken cancellationToken = default);
}
