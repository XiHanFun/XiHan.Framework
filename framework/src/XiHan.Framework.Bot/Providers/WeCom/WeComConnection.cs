#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WeComConnection
// Guid:e090aec3-2ede-4510-8ec2-6e542f34c26d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/05 01:08:45
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Bot.Providers.WeCom;

/// <summary>
/// WeChatConnection
/// </summary>
public class WeComConnection
{
    private const string DefaultWeComWebHookUrl = "https://qyapi.weixin.qq.com/cgi-bin/webhook/send";

    private const string DefaultWeComUploadUrl = "https://qyapi.weixin.qq.com/cgi-bin/webhook/upload_media";

    private string? _webHookUrl;

    private string? _uploadUrl;

    /// <summary>
    /// 网络挂钩地址
    /// </summary>
    public string WebHookUrl
    {
        get => _webHookUrl ??= DefaultWeComWebHookUrl;
        set => _webHookUrl = value;
    }

    /// <summary>
    /// 文件上传地址
    /// </summary>
    public string UploadUrl
    {
        get => _uploadUrl ??= DefaultWeComUploadUrl;
        set => _uploadUrl = value;
    }

    /// <summary>
    /// 访问令牌
    /// </summary>
    public string Key { get; set; } = string.Empty;
}
