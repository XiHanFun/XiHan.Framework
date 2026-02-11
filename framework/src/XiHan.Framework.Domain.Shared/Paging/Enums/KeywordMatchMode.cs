#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:KeywordMatchMode
// Guid:8f49aa7d-35b3-4c28-854e-e53a9d66c538
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/02 13:11:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel;

namespace XiHan.Framework.Domain.Shared.Paging.Enums;

/// <summary>
/// 关键字匹配模式
/// </summary>
public enum KeywordMatchMode
{
    /// <summary>
    /// 包含（LIKE %x%）
    /// </summary>
    [Description("包含")]
    Contains = 1000,

    /// <summary>
    /// 前缀匹配（LIKE x%）
    /// </summary>
    [Description("前缀匹配")]
    StartsWith = 1001,

    /// <summary>
    /// 后缀匹配（LIKE %x）
    /// </summary>
    [Description("后缀匹配")]
    EndsWith = 1002,

    /// <summary>
    /// 完全匹配
    /// </summary>
    [Description("完全匹配")]
    Exact = 1003
}
