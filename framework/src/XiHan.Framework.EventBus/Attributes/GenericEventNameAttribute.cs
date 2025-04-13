#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:GenericEventNameAttribute
// Guid:fbbebae9-1590-44c6-93bf-188751e032ce
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/4/13 13:34:53
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Utils.Text;

namespace XiHan.Framework.EventBus.Attributes;

/// <summary>
/// 通用事件名称属性
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class GenericEventNameAttribute : Attribute, IEventNameProvider
{
    /// <summary>
    /// 事件名称前缀
    /// </summary>
    public string? Prefix { get; set; }

    /// <summary>
    /// 事件名称后缀
    /// </summary>
    public string? Postfix { get; set; }

    /// <summary>
    /// 获取事件名称
    /// </summary>
    /// <param name="eventType"></param>
    /// <returns></returns>
    /// <exception cref="XiHanException"></exception>
    public virtual string GetName(Type eventType)
    {
        if (!eventType.IsGenericType)
        {
            throw new XiHanException($"Given type is not generic: {eventType.AssemblyQualifiedName}");
        }

        var genericArguments = eventType.GetGenericArguments();
        if (genericArguments.Length > 1)
        {
            throw new XiHanException($"Given type has more than one generic argument: {eventType.AssemblyQualifiedName}");
        }

        var eventName = EventNameAttribute.GetNameOrDefault(genericArguments[0]);

        if (!Prefix.IsNullOrEmpty())
        {
            eventName = Prefix + eventName;
        }

        if (!Postfix.IsNullOrEmpty())
        {
            eventName = eventName + Postfix;
        }

        return eventName;
    }
}
