#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SmsProviderType
// Guid:ae3c36e6-b002-4437-b6bb-4643c213002e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel;

namespace XiHan.Framework.Bot.Sms;

/// <summary>
/// 短信服务商类型
/// </summary>
public enum SmsProviderType
{
    /// <summary>
    /// 阿里云
    /// </summary>
    [Description("阿里云")]
    Aliyun = 0,

    /// <summary>
    /// 腾讯云
    /// </summary>
    [Description("腾讯云")]
    TencentCloud = 1
}
