// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Caching.Distributed.Abstracts;

/// <summary>
/// IDistributedCacheSerializer
/// </summary>
public interface IDistributedCacheSerializer
{
    /// <summary>
    /// 序列化
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="obj"></param>
    /// <returns></returns>
    byte[] Serialize<T>(T obj);

    /// <summary>
    /// 反序列化
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="bytes"></param>
    /// <returns></returns>
    T Deserialize<T>(byte[] bytes);
}
