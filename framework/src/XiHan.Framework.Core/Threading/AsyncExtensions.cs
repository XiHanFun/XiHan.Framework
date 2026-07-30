// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;

namespace XiHan.Framework.Core.Threading;

/// <summary>
/// 异步扩展方法
/// </summary>
public static class AsyncExtensions
{
    /// <summary>
    /// 检查给定方法是否为异步方法
    /// </summary>
    /// <param name="method">要检查的方法</param>
    public static bool IsAsync(this MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method, nameof(method));

        return method.ReturnType.IsTaskOrTaskOfT();
    }

    /// <summary>
    /// 检查给定的 Type 类型是否是 Task 类型或者泛型 Task T 类型
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool IsTaskOrTaskOfT(this Type type)
    {
        return type == typeof(Task) || (type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>));
    }

    /// <summary>
    /// 检查给定的 Type 类型是否是泛型 Task T 类型
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool IsTaskOfT(this Type type)
    {
        return type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>);
    }
}
