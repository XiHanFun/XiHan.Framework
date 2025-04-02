#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IUnitOfWorkEventPublisher
// Guid:79938703-3ec4-45ed-bcb6-dbee5c995b62
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/4/1 20:48:43
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Uow.Abstracts;

/// <summary>
/// 工作单元事件发布者
/// </summary>
public interface IUnitOfWorkEventPublisher
{
    /// <summary>
    /// 发布本地事件
    /// </summary>
    /// <param name="localEvents"></param>
    /// <returns></returns>
    Task PublishLocalEventsAsync(IEnumerable<UnitOfWorkEventRecord> localEvents);

    /// <summary>
    /// 发布分布式事件
    /// </summary>
    /// <param name="distributedEvents"></param>
    /// <returns></returns>
    Task PublishDistributedEventsAsync(IEnumerable<UnitOfWorkEventRecord> distributedEvents);
}
