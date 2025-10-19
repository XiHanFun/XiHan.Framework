#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EventHandlerInvokerCacheItem
// Guid:d1eecf14-3c09-4fff-a19f-99f19cc07d48
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 7:34:09
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.EventBus;

/// <summary>
/// 事件处理器调用缓存项
/// </summary>
public class EventHandlerInvokerCacheItem
{
    /// <summary>
    /// 本地事件处理器执行器
    /// </summary>
    public IEventHandlerMethodExecutor? Local { get; set; }

    /// <summary>
    /// 分布式事件处理器执行器
    /// </summary>
    public IEventHandlerMethodExecutor? Distributed { get; set; }
}
