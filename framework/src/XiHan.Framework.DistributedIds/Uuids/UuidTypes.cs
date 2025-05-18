#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UuidTypes
// Guid:ff7c2ee4-96dd-4ee7-bec1-bd26d60d745a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/18 20:27:56
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.DistributedIds.Uuids;

/// <summary>
/// UUID生成器类型
/// </summary>
public enum UuidTypes
{
    /// <summary>
    /// 标准UUID，随机生成
    /// </summary>
    Standard = 0,

    /// <summary>
    /// 顺序UUID，包含时间戳，用于排序
    /// </summary>
    Sequential = 1,

    /// <summary>
    /// 基于时间的UUID (v1)
    /// </summary>
    TimeBasedV1 = 2,

    /// <summary>
    /// 基于名称和MD5的UUID (v3)
    /// </summary>
    NameBasedMD5 = 3,

    /// <summary>
    /// 随机生成的UUID (v4)
    /// </summary>
    RandomV4 = 4,

    /// <summary>
    /// 基于名称和SHA1的UUID (v5)
    /// </summary>
    NameBasedSHA1 = 5
}
