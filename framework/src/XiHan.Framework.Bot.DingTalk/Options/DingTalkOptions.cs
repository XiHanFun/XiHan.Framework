// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Bot.DingTalk.Options;

/// <summary>
/// 钉钉提供者配置
/// </summary>
public class DingTalkOptions
{
    private const string DefaultDingTalkWebHookUrl = "https://oapi.dingtalk.com/robot/send";

    private string? _webHookUrl;

    /// <summary>
    /// 是否启用该提供者
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 网络挂钩地址
    /// </summary>
    public string WebHookUrl
    {
        get => _webHookUrl ??= DefaultDingTalkWebHookUrl;
        set => _webHookUrl = value;
    }

    /// <summary>
    /// 访问令牌
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// 机密
    /// </summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// 关键字
    /// </summary>
    public string? KeyWord { get; set; }
}
