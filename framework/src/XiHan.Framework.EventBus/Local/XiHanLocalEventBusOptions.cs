#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanLocalEventBusOptions
// Guid:e896a0ac-0dff-47b5-b43e-1132b4c14ef2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 5:15:23
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Collections;
using XiHan.Framework.EventBus.Abstractions;

namespace XiHan.Framework.EventBus.Local;

/// <summary>
/// 本地事件总线选项
/// </summary>
public class XiHanLocalEventBusOptions
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public XiHanLocalEventBusOptions()
    {
        Handlers = new TypeList<IEventHandler>();
    }

    /// <summary>
    /// 事件处理器列表
    /// </summary>
    public ITypeList<IEventHandler> Handlers { get; }
}
