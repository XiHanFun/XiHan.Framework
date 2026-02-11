#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LocalEventHandlerOrderAttribute
// Guid:4ce0af8b-e586-4e7d-b76d-830cc242070d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 05:21:03
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
