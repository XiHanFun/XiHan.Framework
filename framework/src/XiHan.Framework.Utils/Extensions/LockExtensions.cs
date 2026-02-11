#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LockExtensions
// Guid:d22fe03a-3e63-423d-a9fd-7bb6777d90a5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/09 06:10:58
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Extensions;

/// <summary>
/// 锁扩展方法
/// </summary>
public static class LockExtensions
{
    #region 基础锁定

    /// <summary>
    /// 对某个对象加锁并执行操作
    /// </summary>
    /// <param name="lockObj"></param>
    /// <param name="action"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public static void Lock(this object lockObj, Action action)
    {
        ArgumentNullException.ThrowIfNull(lockObj);
        ArgumentNullException.ThrowIfNull(action);

        lock (lockObj)
        {
            action();
        }
    }

    /// <summary>
    /// 对某个对象加锁并执行操作，返回结果
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="lockObj"></param>
    /// <param name="func"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static T Lock<T>(this object lockObj, Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(lockObj);
        ArgumentNullException.ThrowIfNull(func);

        lock (lockObj)
        {
            return func();
        }
    }

    #endregion 基础锁定

    #region ReaderWriterLockSlim扩展

    /// <summary>
    /// 使用ReaderWriterLockSlim进行只读操作
    /// </summary>
    /// <param name="lockSlim"></param>
    /// <param name="action"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public static void ReadLock(this ReaderWriterLockSlim lockSlim, Action action)
    {
        ArgumentNullException.ThrowIfNull(lockSlim);
        ArgumentNullException.ThrowIfNull(action);

        lockSlim.EnterReadLock();
        try
        {
            action();
        }
        finally
        {
            lockSlim.ExitReadLock();
        }
    }

    /// <summary>
    /// 使用ReaderWriterLockSlim进行只读操作并返回结果
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="lockSlim"></param>
    /// <param name="func"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static T ReadLock<T>(this ReaderWriterLockSlim lockSlim, Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(lockSlim);
        ArgumentNullException.ThrowIfNull(func);

        lockSlim.EnterReadLock();
        try
        {
            return func();
        }
        finally
        {
            lockSlim.ExitReadLock();
        }
    }

    /// <summary>
    /// 使用ReaderWriterLockSlim进行写操作
    /// </summary>
    /// <param name="lockSlim"></param>
    /// <param name="action"></param>
    public static void WriteLock(this ReaderWriterLockSlim lockSlim, Action action)
    {
        ArgumentNullException.ThrowIfNull(lockSlim);
        ArgumentNullException.ThrowIfNull(action);

        lockSlim.EnterWriteLock();
        try
        {
            action();
        }
        finally
        {
            lockSlim.ExitWriteLock();
        }
    }

    /// <summary>
    /// 使用ReaderWriterLockSlim进行写操作并返回结果
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="lockSlim"></param>
    /// <param name="func"></param>
    /// <returns></returns>
    public static T WriteLock<T>(this ReaderWriterLockSlim lockSlim, Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(lockSlim);
        ArgumentNullException.ThrowIfNull(func);

        lockSlim.EnterWriteLock();
        try
        {
            return func();
        }
        finally
        {
            lockSlim.ExitWriteLock();
        }
    }

    #endregion ReaderWriterLockSlim扩展

    #region 超时锁定

    /// <summary>
    /// 带超时的锁定操作
    /// </summary>
    /// <param name="lockObj"></param>
    /// <param name="timeout"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    public static bool TryLock(this object lockObj, TimeSpan timeout, Action action)
    {
        ArgumentNullException.ThrowIfNull(lockObj);
        ArgumentNullException.ThrowIfNull(action);

        var lockTaken = false;
        try
        {
            Monitor.TryEnter(lockObj, timeout, ref lockTaken);
            if (lockTaken)
            {
                action();
                return true;
            }
        }
        finally
        {
            if (lockTaken)
            {
                Monitor.Exit(lockObj);
            }
        }

        return false;
    }

    /// <summary>
    /// 带超时的锁定操作，返回结果
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="lockObj"></param>
    /// <param name="timeout"></param>
    /// <param name="func"></param>
    /// <returns></returns>
    public static (bool Success, T? Result) TryLock<T>(this object lockObj, TimeSpan timeout, Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(lockObj);
        ArgumentNullException.ThrowIfNull(func);

        var lockTaken = false;
        try
        {
            Monitor.TryEnter(lockObj, timeout, ref lockTaken);
            if (lockTaken)
            {
                return (true, func());
            }
        }
        finally
        {
            if (lockTaken)
            {
                Monitor.Exit(lockObj);
            }
        }

        return (false, default);
    }

    #endregion 超时锁定
}
