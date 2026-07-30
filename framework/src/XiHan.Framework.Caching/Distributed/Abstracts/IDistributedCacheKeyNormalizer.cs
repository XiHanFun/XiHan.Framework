// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Caching.Distributed.Abstracts;

/// <summary>
/// 分布式缓存键规范化器
/// </summary>
public interface IDistributedCacheKeyNormalizer
{
    /// <summary>
    /// 规范化键
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    string NormalizeKey(DistributedCacheKeyNormalizeArgs args);
}
