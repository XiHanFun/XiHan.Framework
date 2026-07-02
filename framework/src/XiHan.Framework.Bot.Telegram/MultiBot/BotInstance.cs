#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BotInstance
// Guid:556ae81b-4de4-40e2-9d26-54d6db3b76fa
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Net;
using Telegram.Bot;
using XiHan.Framework.Bot.Telegram.Options;

namespace XiHan.Framework.Bot.Telegram.MultiBot;

/// <summary>
/// 单个 Telegram 机器人运行实例（客户端按网络配置构建：代理 / 自建 Bot API Server / 超时）
/// </summary>
public sealed class BotInstance : IDisposable
{
    private readonly HttpClient _httpClient;
    private bool _disposed;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="config">机器人配置</param>
    /// <param name="network">网络配置</param>
    public BotInstance(TelegramBotConfig config, TelegramBotNetworkOptions? network = null)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (string.IsNullOrWhiteSpace(config.Name))
        {
            throw new ArgumentException("Bot Name 不能为空。", nameof(config));
        }

        if (string.IsNullOrWhiteSpace(config.Token))
        {
            throw new ArgumentException($"Bot({config.Name}) Token 不能为空。", nameof(config));
        }

        Name = config.Name.Trim();
        Token = config.Token.Trim();
        Config = config;

        network ??= new TelegramBotNetworkOptions();
        _httpClient = BuildHttpClient(network);

        var baseUrl = network.BaseUrl?.Trim();
        var clientOptions = string.IsNullOrWhiteSpace(baseUrl)
            ? new TelegramBotClientOptions(Token)
            : new TelegramBotClientOptions(Token, baseUrl);
        Client = new TelegramBotClient(clientOptions, _httpClient);
    }

    /// <summary>
    /// 机器人名称（业务侧以该名称定位机器人）
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 机器人 Token
    /// </summary>
    public string Token { get; }

    /// <summary>
    /// Telegram 客户端
    /// </summary>
    public ITelegramBotClient Client { get; }

    /// <summary>
    /// Telegram Bot Id（启动后由 GetMe 回填）
    /// </summary>
    public long BotId { get; private set; }

    /// <summary>
    /// Telegram Bot 用户名（不含 @，启动后由 GetMe 回填）
    /// </summary>
    public string Username { get; private set; } = string.Empty;

    /// <summary>
    /// 对应配置
    /// </summary>
    public TelegramBotConfig Config { get; }

    /// <summary>
    /// 设置 Telegram 返回的机器人身份信息
    /// </summary>
    /// <param name="botId">Bot Id</param>
    /// <param name="username">用户名</param>
    public void SetIdentity(long botId, string? username)
    {
        BotId = botId;
        Username = NormalizeUsername(username);
    }

    /// <summary>
    /// 判断用户是否为该机器人管理员（来自机器人配置）
    /// </summary>
    /// <param name="userId">用户 Id</param>
    /// <returns>是否管理员</returns>
    public bool IsAdmin(long userId)
    {
        if (userId <= 0 || Config.AdminUsers is not { Length: > 0 })
        {
            return false;
        }

        return Config.AdminUsers.Contains(userId);
    }

    /// <summary>
    /// 判断群组/频道是否允许使用该机器人（白名单同时覆盖群组与频道；频道 ChatId 与超级群同为 -100 前缀）。
    /// <para><b>fail-closed 语义：白名单为空 = 拒收所有群组与频道消息</b></para>
    /// </summary>
    /// <param name="chatId">群组/频道 ChatId</param>
    /// <returns>是否允许</returns>
    public bool IsGroupAllowed(long chatId)
    {
        if (chatId == 0)
        {
            return false;
        }

        var allowList = Config.AllowedGroupChatIds ?? [];
        return allowList.Length != 0 && allowList.Contains(chatId);
    }

    /// <summary>
    /// 释放底层 HttpClient
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _httpClient.Dispose();
    }

    private static HttpClient BuildHttpClient(TelegramBotNetworkOptions network)
    {
        var handler = new HttpClientHandler();
        var proxyUrl = network.ProxyUrl?.Trim();
        if (!string.IsNullOrWhiteSpace(proxyUrl))
        {
            handler.Proxy = BuildProxy(proxyUrl);
            handler.UseProxy = true;
        }

        var timeoutSeconds = network.TimeoutSeconds > 0 ? network.TimeoutSeconds : 100;
        return new HttpClient(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };
    }

    private static WebProxy BuildProxy(string proxyUrl)
    {
        var proxyUri = new Uri(proxyUrl);
        var proxy = new WebProxy(proxyUri);

        // 代理地址携带 userinfo（如 http://user:pass@127.0.0.1:7890）时解析为代理凭证
        if (!string.IsNullOrEmpty(proxyUri.UserInfo))
        {
            var parts = proxyUri.UserInfo.Split(':', 2);
            var userName = Uri.UnescapeDataString(parts[0]);
            var password = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            proxy.Credentials = new NetworkCredential(userName, password);
        }

        return proxy;
    }

    private static string NormalizeUsername(string? username)
    {
        var value = (username ?? string.Empty).Trim();
        return value.StartsWith('@') ? value[1..] : value;
    }
}
