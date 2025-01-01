#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IDistributedCacheKeyNormalizer
// Guid:6eb4977b-85ff-4412-a892-62bf276ad779
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/13 5:42:54
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Caching;

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
