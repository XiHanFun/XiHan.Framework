// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Bot.Telegram.Options;

/// <summary>
/// Telegram 机器人网络配置（代理、自建 Bot API Server、超时）
/// </summary>
public class TelegramBotNetworkOptions
{
    /// <summary>
    /// 代理地址（如 http://127.0.0.1:7890 或 socks5://127.0.0.1:1080），空表示直连
    /// </summary>
    public string ProxyUrl { get; set; } = string.Empty;

    /// <summary>
    /// 自建 Bot API Server 基础地址（如 https://tg-api.example.com），空表示官方 api.telegram.org
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// 请求超时秒数（小于等于 0 时按 100 秒处理）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 100;

    /// <summary>
    /// 比较网络配置是否一致（变更需要重建客户端）
    /// </summary>
    /// <param name="other">对比配置</param>
    /// <returns>是否一致</returns>
    public bool IsSameAs(TelegramBotNetworkOptions? other)
    {
        if (other is null)
        {
            return false;
        }

        return string.Equals(ProxyUrl?.Trim(), other.ProxyUrl?.Trim(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(BaseUrl?.Trim(), other.BaseUrl?.Trim(), StringComparison.OrdinalIgnoreCase)
            && TimeoutSeconds == other.TimeoutSeconds;
    }
}
