#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SequentialGuidType
// Guid:5ee48d19-4309-4bce-80e0-bf5b6b70d458
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 5:37:38
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.DistributedIds.Guids;

/// <summary>
/// 顺序 Guid 生成类型
/// </summary>
public enum SequentialGuidType
{
    /// <summary>
    /// 字符串形式
    /// </summary>
    SequentialAsString,

    /// <summary>
    /// 二进制形式
    /// </summary>
    SequentialAsBinary,

    /// <summary>
    /// 末尾形式
    /// </summary>
    SequentialAtEnd
}
