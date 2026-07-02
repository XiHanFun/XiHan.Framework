#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TelegramBotSettings
// Guid:6517d525-32fa-4cfc-b079-83d290791992
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Bot.Telegram.Platform.Options;

/// <summary>
/// Telegram 机器人平台全局设置
/// </summary>
public class TelegramBotSettings
{
    /// <summary>
    /// 是否启用 Telegram 机器人平台（默认关闭；未启用时管理器空转，不拉起任何机器人）
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 配置列表缓存秒数（供应用层数据库配置存储实现使用）
    /// </summary>
    public int ConfigCacheSeconds { get; set; } = 5;

    /// <summary>
    /// 管理器刷新间隔秒数（动态增删改机器人的探测周期）
    /// </summary>
    public int ManagerRefreshSeconds { get; set; } = 5;

    /// <summary>
    /// Webhook 基础地址（如 https://example.com）；空表示使用长轮询（Polling）模式
    /// </summary>
    public string WebhookBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Webhook 路由前缀
    /// </summary>
    public string WebhookRoutePrefix { get; set; } = TelegramBotPlatformConsts.DefaultWebhookRoutePrefix;

    /// <summary>
    /// Webhook 密钥令牌；<b>Webhook 模式（WebhookBaseUrl 非空）下必填</b>：
    /// 注册 Webhook 会携带 secret_token，中间件强制校验 X-Telegram-Bot-Api-Secret-Token 请求头，不匹配返回 401；
    /// 未配置时拒绝注册 Webhook 且中间件拒绝所有 Webhook 请求（fail-closed，空不再表示不校验）
    /// </summary>
    public string WebhookSecretToken { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用兜底回复（无任何处理器命中普通消息时回复提示文案；与单机器人配置任一开启即生效）
    /// </summary>
    public bool EnableFallbackReply { get; set; }

    /// <summary>
    /// 网络配置（代理、自建 Bot API Server、超时；变更后管理器会重建全部机器人客户端）
    /// </summary>
    public TelegramBotNetworkOptions Network { get; set; } = new();
}
