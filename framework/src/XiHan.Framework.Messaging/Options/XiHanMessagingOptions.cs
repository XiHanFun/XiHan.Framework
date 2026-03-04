#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanMessagingOptions
// Guid:f16f84d8-fcf4-45de-beb2-f91bc065d227
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/04 15:03:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Messaging.Options;

/// <summary>
/// 消息模块配置
/// </summary>
public class XiHanMessagingOptions
{
    /// <summary>
    /// 是否在单个接收人失败后继续发送
    /// </summary>
    public bool ContinueOnError { get; set; } = true;

    /// <summary>
    /// 找不到发送器时是否抛出异常
    /// </summary>
    public bool ThrowWhenNoSender { get; set; } = false;
}
