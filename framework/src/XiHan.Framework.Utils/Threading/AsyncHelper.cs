#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AsyncHelper
// Guid:135e9177-a2ea-4055-a489-460bd81fdc81
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/5/7 0:41:31
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Utils.Threading;

/// <summary>
/// 与异步方法协作的辅助方法
/// </summary>
public static class AsyncHelper
{
    /// <summary>
    /// 检查给定方法是否为异步方法
    /// </summary>
    /// <param name="method">要检查的方法</param>
    public static bool IsAsync(this MethodInfo method)
    {
        _ = CheckHelper.NotNull(method, nameof(method));

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

    /// <summary>
    /// 如果给定类型是 Task，则返回 void。如果给定类型是 Task{T}，则返回 T。否则返回给定类型。
    /// </summary>
    public static Type UnwrapTask(Type type)
    {
        _ = CheckHelper.NotNull(type, nameof(type));

        return type == typeof(Task) ? typeof(void) : type.IsTaskOfT() ? type.GenericTypeArguments[0] : type;
    }

    /// <summary>
    /// 同步运行异步方法
    /// </summary>
    /// <param name="func">返回结果的函数</param>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <returns>异步操作的结果</returns>
    public static TResult RunSync<TResult>(Func<Task<TResult>> func)
    {
        return Task.Run(func).GetAwaiter().GetResult();
    }

    /// <summary>
    /// 同步运行异步方法
    /// </summary>
    /// <param name="action">异步操作</param>
    public static void RunSync(Func<Task> action)
    {
        Task.Run(action).GetAwaiter().GetResult();
    }
}