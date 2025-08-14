#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IDomainEvents
// Guid:384e101e-98d9-4be7-946b-5584ba900706
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/20 4:40:46
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Domain.Domain.Events;

/// <summary>
/// 领域事件接口
/// </summary>
public interface IDomainEvents
{
    /// <summary>
    /// 获取本地事件
    /// </summary>
    /// <returns></returns>
    IEnumerable<DomainEventRecord> GetLocalEvents();

    /// <summary>
    /// 获取分布式事件
    /// </summary>
    /// <returns></returns>
    IEnumerable<DomainEventRecord> GetDistributedEvents();

    /// <summary>
    /// 清空本地事件
    /// </summary>
    void ClearLocalEvents();

    /// <summary>
    /// 清空分布式事件
    /// </summary>
    void ClearDistributedEvents();
}
