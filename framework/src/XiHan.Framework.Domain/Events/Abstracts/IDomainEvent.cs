#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IDomainEvent
// Guid:tuv12f9d-8e3a-4b5c-9a2e-1234567890tu
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/12 16:10:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Domain.Events.Abstracts;

/// <summary>
/// 领域事件接口
/// 标记接口，用于标识领域事件
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// 事件发生时间
    /// </summary>
    DateTimeOffset OccurredOn { get; }

    /// <summary>
    /// 事件唯一标识
    /// </summary>
    Guid EventId { get; }
}
