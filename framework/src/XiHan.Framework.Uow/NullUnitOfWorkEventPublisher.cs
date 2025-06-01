#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NullUnitOfWorkEventPublisher
// Guid:0000bc1b-4796-4fac-8495-4f311c014063
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/2 4:59:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Uow.Abstracts;

namespace XiHan.Framework.Uow;

/// <summary>
/// 默认空工作单元事件发布者
/// </summary>
/// <remarks>
/// 提供 IUnitOfWorkEventPublisher 的默认实现
/// 如果项目中使用了专门的事件总线(如 EventBus 模块)，则会被替换
/// </remarks>
public class NullUnitOfWorkEventPublisher : IUnitOfWorkEventPublisher, ISingletonDependency
{
    /// <summary>
    /// 发布本地事件
    /// </summary>
    /// <param name="localEvents">本地事件记录</param>
    /// <returns></returns>
    public Task PublishLocalEventsAsync(IEnumerable<UnitOfWorkEventRecord> localEvents)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 发布分布式事件
    /// </summary>
    /// <param name="distributedEvents">分布式事件记录</param>
    /// <returns></returns>
    public Task PublishDistributedEventsAsync(IEnumerable<UnitOfWorkEventRecord> distributedEvents)
    {
        return Task.CompletedTask;
    }
}
