#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IEventHandlerDisposeWrapper
// Guid:4928bc12-f40c-4105-8d57-31d95e5c2571
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/2 6:57:40
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.EventBus.Abstractions;

/// <summary>
/// 事件处理器包装接口
/// </summary>
public interface IEventHandlerDisposeWrapper : IDisposable
{
    /// <summary>
    /// 获取事件处理器实例
    /// </summary>
    IEventHandler EventHandler { get; }
}
