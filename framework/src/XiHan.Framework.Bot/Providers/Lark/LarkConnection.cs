#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LarkConnection
// Guid:f2365168-e192-48e0-ac99-32925512f628
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/05 01:08:45
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Bot.Providers.Lark;

/// <summary>
/// LarkConnection
/// </summary>
public class LarkConnection
{
    private const string DefaultLarkWebHookUrl = "https://open.feishu.cn/open-apis/bot/v2/hook";

    private const string DefaultLarkUploadUrl = "https://open.feishu.cn/open-apis/im/v1/images";

    private string? _webHookUrl;

    private string? _uploadUrl;

    /// <summary>
    /// 网络挂钩地址
    /// </summary>
    public string WebHookUrl
    {
        get => _webHookUrl ??= DefaultLarkWebHookUrl;
        set => _webHookUrl = value;
    }

    /// <summary>
    /// 文件上传地址
    /// </summary>
    public string UploadUrl
    {
        get => _uploadUrl ??= DefaultLarkUploadUrl;
        set => _uploadUrl = value;
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
