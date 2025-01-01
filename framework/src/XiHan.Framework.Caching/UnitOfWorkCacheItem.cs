#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UnitOfWorkCacheItem
// Guid:363555db-3648-4061-bbe9-501b9d80c582
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/13 5:50:02
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Caching;

/// <summary>
/// 工作单元缓存项
/// </summary>
/// <typeparam name="TValue"></typeparam>
[Serializable]
public class UnitOfWorkCacheItem<TValue>
    where TValue : class
{
    /// <summary>
    /// 是否已移除
    /// </summary>
    public bool IsRemoved { get; set; }

    /// <summary>
    /// 值
    /// </summary>
    public TValue? Value { get; set; }

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
}
