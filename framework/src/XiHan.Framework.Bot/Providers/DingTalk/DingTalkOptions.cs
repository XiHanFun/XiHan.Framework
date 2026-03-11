#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DingTalkOptions
// Guid:61e8c752-12f5-449b-bb0a-09bbd0afa6ef
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/05 01:08:45
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Bot.Providers.DingTalk;

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
