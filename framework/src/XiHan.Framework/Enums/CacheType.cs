#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CacheType
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5d3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Enums;

/// <summary>
/// 缓存类型
/// </summary>
public enum CacheType
{
    /// <summary>
    /// 内存缓存
    /// </summary>
    Memory = 0,

    /// <summary>
    /// Redis缓存
    /// </summary>
    Redis = 1,

    /// <summary>
    /// 分布式缓存
    /// </summary>
    Distributed = 2,

    /// <summary>
    /// 文件缓存
    /// </summary>
    File = 3
}
