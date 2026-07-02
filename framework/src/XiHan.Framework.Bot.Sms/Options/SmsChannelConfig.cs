#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SmsChannelConfig
// Guid:e505bca1-c33b-4957-a074-cad54b11c496
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Bot.Sms.Abstractions;
using XiHan.Framework.Bot.Sms.Enums;

namespace XiHan.Framework.Bot.Sms.Options;

/// <summary>
/// 短信通道配置（框架自有模型，由 <see cref="ISmsConfigStore"/> 提供）
/// </summary>
/// <remarks>
/// 凭证解密责任在 store 实现内：<see cref="AccessKeySecret"/> 必须是已解密明文，
/// 框架侧不感知任何加密方案（DataProtection Purpose 等）。
/// </remarks>
public class SmsChannelConfig
{
    /// <summary>
    /// 配置标识（解析器按此键缓存已构建的网关客户端）
    /// </summary>
    public long ConfigId { get; set; }

    /// <summary>
    /// 短信服务商
    /// </summary>
    public SmsProviderType Provider { get; set; } = SmsProviderType.Aliyun;

    /// <summary>
    /// 访问密钥ID（阿里云 AccessKeyId / 腾讯云 SecretId）
    /// </summary>
    public string AccessKeyId { get; set; } = string.Empty;

    /// <summary>
    /// 访问密钥（阿里云 AccessKeySecret / 腾讯云 SecretKey；已解密明文，由 store 实现负责解密）
    /// </summary>
    public string AccessKeySecret { get; set; } = string.Empty;

    /// <summary>
    /// 应用ID（腾讯云 SmsSdkAppId，腾讯云必填；阿里云不使用）
    /// </summary>
    public string? SdkAppId { get; set; }

    /// <summary>
    /// 短信签名（服务商控制台审核通过的签名名称）
    /// </summary>
    public string SignName { get; set; } = string.Empty;

    /// <summary>
    /// 地域（腾讯云必填，如 ap-guangzhou；阿里云不使用，端点固定）
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// 模板映射（JSON：内部模板码 → 服务商模板码 + 参数序）
    /// </summary>
    /// <remarks>
    /// 形如 {"auth-sms-login-code":{"templateCode":"SMS_123456","paramOrder":["code","minutes"]}}；
    /// 阿里云按命名 JSON 参数发送（参数名须与服务商模板变量名一致），paramOrder 供腾讯云位置参数数组使用。
    /// </remarks>
    public string? TemplateMap { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
