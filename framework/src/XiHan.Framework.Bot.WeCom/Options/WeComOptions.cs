// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Bot.WeCom.Options;

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
