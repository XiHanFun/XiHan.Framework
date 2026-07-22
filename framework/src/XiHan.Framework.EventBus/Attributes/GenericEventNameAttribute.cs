// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.Utils.Extensions;

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
            throw new XiHanException($"{eventType.AssemblyQualifiedName} 类型不是泛型类型");
        }

        var genericArguments = eventType.GetGenericArguments();
        if (genericArguments.Length > 1)
        {
            throw new XiHanException($"{eventType.AssemblyQualifiedName} 类型具有多个泛型参数");
        }

        var eventName = EventNameAttribute.GetNameOrDefault(genericArguments[0]);

        if (!Prefix.IsNullOrEmpty())
        {
            eventName = Prefix + eventName;
        }

        if (!Postfix.IsNullOrEmpty())
        {
            eventName += Postfix;
        }

        return eventName;
    }
}
