// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Uow;

/// <summary>
/// 工作单元缓存项
/// </summary>
/// <typeparam name="TValue"></typeparam>
public class UnitOfWorkCacheItem<TValue>
    where TValue : class
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public UnitOfWorkCacheItem()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="value"></param>
    public UnitOfWorkCacheItem(TValue value)
    {
        Value = value;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="value"></param>
    /// <param name="isRemoved"></param>
    public UnitOfWorkCacheItem(TValue value, bool isRemoved)
    {
        Value = value;
        IsRemoved = isRemoved;
    }

    /// <summary>
    /// 是否已移除
    /// </summary>
    public bool IsRemoved { get; set; }

    /// <summary>
    /// 值
    /// </summary>
    public TValue? Value { get; set; }

    /// <summary>
    /// 设置值
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public UnitOfWorkCacheItem<TValue> SetValue(TValue value)
    {
        Value = value;
        IsRemoved = false;
        return this;
    }

    /// <summary>
    /// 移除值
    /// </summary>
    /// <returns></returns>
    public UnitOfWorkCacheItem<TValue> RemoveValue()
    {
        Value = null;
        IsRemoved = true;
        return this;
    }

    /// <summary>
    /// 获取未移除的值
    /// </summary>
    /// <returns></returns>
    public TValue? GetUnRemovedValueOrNull()
    {
        return !IsRemoved ? Value : null;
    }
}
