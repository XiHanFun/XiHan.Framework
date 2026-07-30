// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.DistributedIds.SnowflakeIds;

/// <summary>
/// 雪花唯一标识类型
/// </summary>
public enum SnowflakeIdTypes
{
    /// <summary>
    /// 雪花漂移算法
    /// </summary>
    SnowFlakeMethod = 1,

    /// <summary>
    /// 传统雪花算法
    /// </summary>
    ClassicSnowFlakeMethod = 2
}
