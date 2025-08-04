#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NotificationType
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5dd
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Enums;

/// <summary>
/// 通知类型
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// 邮件
    /// </summary>
    Email = 0,

    /// <summary>
    /// 短信
    /// </summary>
    Sms = 1,

    /// <summary>
    /// 推送
    /// </summary>
    Push = 2,

    /// <summary>
    /// 站内信
    /// </summary>
    InApp = 3
}
