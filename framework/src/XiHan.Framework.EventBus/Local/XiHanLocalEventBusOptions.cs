// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
