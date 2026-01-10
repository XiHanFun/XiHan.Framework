#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IEventNameProvider
// Guid:fd3bb7a4-b2a7-49a3-bbf2-a9d96a69b9c5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/4/13 13:36:46
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.EventBus.Abstractions;

/// <summary>
/// 事件名称提供器接口
/// </summary>
public interface IEventNameProvider
{
    /// <summary>
    /// 获取事件名称
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <returns>事件名称</returns>
    string GetName(Type eventType);
}
