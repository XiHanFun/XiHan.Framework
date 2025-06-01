#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EventNameAttribute
// Guid:702f1a87-72f3-42d7-b7ba-58d15e7934f3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/4/13 13:38:05
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.System;

namespace XiHan.Framework.EventBus.Attributes;

/// <summary>
/// 事件名称属性
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class EventNameAttribute : Attribute, IEventNameProvider
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name"></param>
    public EventNameAttribute(string name)
    {
        Name = Guard.NotNullOrWhiteSpace(name, nameof(name));
    }

    /// <summary>
    /// 事件名称
    /// </summary>
    public virtual string Name { get; }

    /// <summary>
    /// 获取事件名称
    /// </summary>
    /// <typeparam name="TEvent"></typeparam>
    /// <returns></returns>
    public static string GetNameOrDefault<TEvent>()
    {
        return GetNameOrDefault(typeof(TEvent));
    }

    /// <summary>
    /// 获取事件名称
    /// </summary>
    /// <param name="eventType"></param>
    /// <returns></returns>
    public static string GetNameOrDefault(Type eventType)
    {
        Guard.NotNull(eventType, nameof(eventType));

        return (eventType
                    .GetCustomAttributes(true)
                    .OfType<IEventNameProvider>()
                    .FirstOrDefault()
                    ?.GetName(eventType)
                ?? eventType.FullName)!;
    }

    /// <summary>
    /// 获取事件名称
    /// </summary>
    /// <param name="eventType"></param>
    /// <returns></returns>
    public string GetName(Type eventType)
    {
        return Name;
    }
}
