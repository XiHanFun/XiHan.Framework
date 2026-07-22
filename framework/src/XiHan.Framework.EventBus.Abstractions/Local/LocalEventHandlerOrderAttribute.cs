// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.EventBus.Abstractions.Local;

/// <summary>
/// 本地事件处理器顺序属性
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class LocalEventHandlerOrderAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="order"></param>
    public LocalEventHandlerOrderAttribute(int order)
    {
        Order = order;
    }

    /// <summary>
    /// 顺序
    /// </summary>
    public int Order { get; set; }
}
