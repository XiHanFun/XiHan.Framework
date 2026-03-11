#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WeComOptions
// Guid:9336f465-97f6-457a-82b0-e19f705f4611
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/05 01:08:45
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Bot.Providers.WeCom;

/// <summary>
/// 企业微信提供者配置
/// </summary>
public class WeComOptions
{
    private const string DefaultWeComWebHookUrl = "https://qyapi.weixin.qq.com/cgi-bin/webhook/send";

    private const string DefaultWeComUploadUrl = "https://qyapi.weixin.qq.com/cgi-bin/webhook/upload_media";

    private string? _webHookUrl;

    private string? _uploadUrl;

    /// <summary>
    /// 是否启用该提供者
    /// </summary>
    public bool Enabled { get; set; } = true;

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
