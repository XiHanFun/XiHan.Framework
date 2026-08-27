// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Telegram.Options;

namespace XiHan.Framework.Bot.Telegram.Tests.Options;

/// <summary>
/// <see cref="TelegramBotPlatformConsts"/> 平台常量测试
/// </summary>
/// <remarks>
/// 这四个常量全部是对外协议：请求头名由 Telegram 服务端发送、回调分隔符决定 callback data 的解析口径、
/// /start 是 Telegram 深链的固定入口。任何一个漂移都会在生产上表现为「机器人突然收不到/认不出消息」。
/// </remarks>
public class TelegramBotPlatformConstsTests
{
    /// <summary>
    /// 默认 Webhook 路由前缀锁死
    /// </summary>
    [Fact]
    public void DefaultWebhookRoutePrefix_IsStable()
    {
        Assert.Equal("/api/telegram-bot/webhook", TelegramBotPlatformConsts.DefaultWebhookRoutePrefix);
    }

    /// <summary>
    /// 密钥令牌请求头名必须与 Telegram Bot API 规定完全一致
    /// </summary>
    [Fact]
    public void SecretTokenHeaderName_MatchesTelegramBotApiSpec()
    {
        Assert.Equal("X-Telegram-Bot-Api-Secret-Token", TelegramBotPlatformConsts.SecretTokenHeaderName);
    }

    /// <summary>
    /// 回调数据分隔符为冒号（callback data 约定 action:id）
    /// </summary>
    [Fact]
    public void CallbackDataSeparator_IsColon()
    {
        Assert.Equal(':', TelegramBotPlatformConsts.CallbackDataSeparator);
    }

    /// <summary>
    /// 深链命令固定为 /start
    /// </summary>
    [Fact]
    public void StartCommand_IsSlashStart()
    {
        Assert.Equal("/start", TelegramBotPlatformConsts.StartCommand);
    }
}
