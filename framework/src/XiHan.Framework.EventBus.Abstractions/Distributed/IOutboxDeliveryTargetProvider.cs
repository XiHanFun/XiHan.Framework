// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.EventBus.Abstractions.Distributed;

/// <summary>
/// 发件箱投递目标目录，由应用实现，列出需要单独扫描发件箱的租户
/// </summary>
/// <remarks>
/// 未注册本接口时，写入租户独立库的入箱一律被拒绝，发送循环只扫描宿主布局。
/// 目录应至少包含全部使用独立数据库布局的租户；与平台共用布局的租户可以不列入。
/// </remarks>
public interface IOutboxDeliveryTargetProvider
{
    /// <summary>
    /// 分页读取目录
    /// </summary>
    /// <remarks>
    /// 发送循环在无租户上下文中调用本方法。
    /// </remarks>
    /// <param name="cursor">上一页返回的游标，读取首页时为 null</param>
    /// <param name="pageSize">单页最多返回的目标数，大于零</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>一页投递目标</returns>
    Task<OutboxDeliveryTargetPage> GetPageAsync(string? cursor, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按租户标识查找投递目标
    /// </summary>
    /// <remarks>
    /// 发件箱在无租户上下文、独立的非事务工作单元中调用本方法，查询所用连接不登记进当前业务工作单元；
    /// 每次租户上下文中的入箱调用一次，建议实现方缓存查询结果。
    /// </remarks>
    /// <param name="tenantId">租户标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>投递目标，不在目录中时为 null</returns>
    Task<OutboxDeliveryTarget?> FindAsync(long tenantId, CancellationToken cancellationToken = default);
}
