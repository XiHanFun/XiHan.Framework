// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
