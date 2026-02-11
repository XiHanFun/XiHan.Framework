#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ILocalEventHandler
// Guid:edc067ef-749c-4df1-bef4-f5e16ebec9d8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/02 06:54:15
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.EventBus.Abstractions.Local;

/// <summary>
/// 本地事件处理器接口
/// </summary>
public interface ILocalEventHandler<in TEvent> : IEventHandler
{
    /// <summary>
    /// 事件处理器通过实现此方法来处理事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    Task HandleEventAsync(TEvent eventData);
}
