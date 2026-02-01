#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LockExtensions
// Guid:6fd50730-fd27-4106-81bf-24695634a1cd
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 05:17:29
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.Threading;

/// <summary>
/// 锁扩展方法
/// </summary>
public static class LockExtensions
{
    /// <summary>
    /// 对某个对象加锁并执行操作
    /// </summary>
    /// <param name="source">对象</param>
    /// <param name="action">操作</param>
    public static void Locking(this object source, Action action)
    {
        lock (source)
        {
            action();
        }
    }

    /// <summary>
    /// 对某个对象加锁并执行操作
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="source">对象</param>
    /// <param name="action">操作</param>
    public static void Locking<T>(this T source, Action<T> action) where T : class
    {
        lock (source)
        {
            action(source);
        }
    }

    /// <summary>
    /// 对某个对象加锁并执行操作
    /// </summary>
    /// <typeparam name="TResult">返回类型</typeparam>
    /// <param name="source">对象</param>
    /// <param name="func">操作</param>
    /// <returns>返回结果</returns>
    public static TResult Locking<TResult>(this object source, Func<TResult> func)
    {
        lock (source)
        {
            return func();
        }
    }

    /// <summary>
    /// 对某个对象加锁并执行操作
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <typeparam name="TResult">返回类型</typeparam>
    /// <param name="source">对象</param>
    /// <param name="func">操作</param>
    /// <returns>返回结果</returns>
    public static TResult Locking<T, TResult>(this T source, Func<T, TResult> func) where T : class
    {
        lock (source)
        {
            return func(source);
        }
    }
}
