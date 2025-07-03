#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:StatusType
// Guid:d4ba61ad-3d63-4413-b0b6-729cd14dc593
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/7/4 2:56:39
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Enums.Enums;

/// <summary>
/// 枚举状态类型
/// </summary>
public enum EnumStatusType
{
    /// <summary>
    /// 激活
    /// </summary>
    Active,

    /// <summary>
    /// 非激活
    /// </summary>
    Inactive,

    /// <summary>
    /// 等待中
    /// </summary>
    Pending,

    /// <summary>
    /// 禁用
    /// </summary>
    Disabled,

    /// <summary>
    /// 错误
    /// </summary>
    Error,

    /// <summary>
    /// 处理中
    /// </summary>
    Processing
}
