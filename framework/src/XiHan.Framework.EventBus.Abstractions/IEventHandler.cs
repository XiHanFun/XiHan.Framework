#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IEventHandler
// Guid:5cf3eb73-ce9f-4833-a40f-3cd754bfb4c9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/02 06:53:33
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Abstractions.Local;

namespace XiHan.Framework.EventBus.Abstractions;

/// <summary>
/// 所有事件处理器的间接基础接口
/// 需要实现 <see cref="ILocalEventHandler{TEvent}"/> 或 <see cref="IDistributedEventHandler{TEvent}"/>，而不是直接实现此接口
/// </summary>
public interface IEventHandler
{
}
