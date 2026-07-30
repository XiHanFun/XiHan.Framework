// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;

namespace XiHan.Framework.Utils.Reflections;

/// <summary>
/// 方法信息扩展方法
/// </summary>
public static class MethodInfoExtensions
{
    /// <summary>
    /// 方法是否是异步
    /// </summary>
    /// <param name="method"></param>
    /// <returns></returns>
    public static bool IsAsync(this MethodInfo method)
    {
        return method.ReturnType == typeof(Task)
            || (method.ReturnType.IsGenericType
            && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>));
    }

    /// <summary>
    /// 返回当前方法信息是否是重写方法
    /// </summary>
    /// <param name="method">要判断的方法信息</param>
    /// <returns>是否是重写方法</returns>
    public static bool IsOverridden(this MethodInfo method)
    {
        return method.GetBaseDefinition().DeclaringType != method.DeclaringType;
    }
}
