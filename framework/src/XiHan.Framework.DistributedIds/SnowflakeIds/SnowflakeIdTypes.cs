#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SnowflakeIdTypes
// Guid:1ca7ad57-1ac9-4cf8-8e1f-cfe2a8f43ec4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/19 2:33:58
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
