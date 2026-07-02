#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TelegramBotWebhookMiddleware
// Guid:334530ac-63e3-4ffd-a72f-db64b255167c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using XiHan.Framework.Bot.Telegram.MultiBot;
using XiHan.Framework.Bot.Telegram.Options;

namespace XiHan.Framework.Bot.Telegram.Webhook;

/// <summary>
/// Telegram Bot Webhook 动态路由中间件（匹配 POST {prefix}/{botName}）
/// </summary>
/// <remarks>
/// Webhook 模式强制要求配置 WebhookSecretToken：未配置一律返回 401（fail-closed，请求体字段可伪造，
/// 不能作为鉴权依据），配置后强制校验 X-Telegram-Bot-Api-Secret-Token 请求头（固定时间比较），不匹配返回 401；
/// 校验通过后将 Update 排入后台分发并立即返回 200（处理不绑请求生命周期，防 Telegram 重发风暴）。
/// </remarks>
public sealed class TelegramBotWebhookMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TelegramBotWebhookMiddleware> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="next">下一个中间件</param>
    /// <param name="logger">日志记录器</param>
    public TelegramBotWebhookMiddleware(RequestDelegate next, ILogger<TelegramBotWebhookMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 处理请求
    /// </summary>
    /// <param name="context">HTTP 上下文</param>
    /// <param name="manager">机器人管理器</param>
    /// <param name="registry">机器人注册表</param>
    public async Task InvokeAsync(HttpContext context, TelegramBotManager manager, BotRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(registry);

        if (!TryMatchWebhookRequest(context.Request, manager.WebhookRoutePrefix, out var botName))
        {
            await _next(context);
            return;
        }

        // ── secret_token 校验：Webhook 模式强制要求非空密钥，未配置一律拒绝（fail-closed），不匹配返回 401 ──
        var expectedSecret = manager.WebhookSecretToken;
        if (string.IsNullOrEmpty(expectedSecret))
        {
            _logger.LogError("Telegram Webhook 拒绝请求：Webhook 模式必须配置 WebhookSecretToken。Bot={BotName}", botName);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var actualSecret = context.Request.Headers[TelegramBotPlatformConsts.SecretTokenHeaderName].ToString();
        if (!FixedTimeEquals(actualSecret, expectedSecret))
        {
            _logger.LogWarning("Telegram Webhook 密钥令牌校验失败。Bot={BotName}", botName);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        Update? update = null;
        try
        {
            update = await JsonSerializer.DeserializeAsync<Update>(
                context.Request.Body,
                JsonBotAPI.Options,
                cancellationToken: context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Telegram Webhook 请求体反序列化失败。Path={Path}", context.Request.Path);
        }

        if (!string.IsNullOrWhiteSpace(botName) && update is not null)
        {
            // 用无锁 TryGet：注册表由管理器维护，热路径不做任何 I/O
            if (!registry.TryGet(botName, out var bot))
            {
                _logger.LogWarning("Telegram Webhook 机器人未找到（未注册或未运行）。Bot={BotName}", botName);
            }
            else
            {
                // 入队后台分发后立即返回 200：处理生命周期与 HTTP 请求解耦，
                // 慢处理器不再被 RequestAborted 半途取消，也不会因 Telegram 超时重发后被幂等标记丢弃
                manager.QueueDispatch(bot, update);
            }
        }

        // 无论处理成败均返回 200，避免 Telegram 对失败响应的重发风暴
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
        }
    }

    private static bool TryMatchWebhookRequest(HttpRequest request, string routePrefix, out string botName)
    {
        botName = string.Empty;
        if (!HttpMethods.IsPost(request.Method))
        {
            return false;
        }

        if (!request.Path.StartsWithSegments(routePrefix, out var remaining))
        {
            return false;
        }

        var value = remaining.Value?.Trim('/');
        if (string.IsNullOrWhiteSpace(value) || value.Contains('/'))
        {
            return false;
        }

        botName = Uri.UnescapeDataString(value);
        return true;
    }

    private static bool FixedTimeEquals(string actual, string expected)
    {
        var actualBytes = Encoding.UTF8.GetBytes(actual ?? string.Empty);
        var expectedBytes = Encoding.UTF8.GetBytes(expected ?? string.Empty);
        return CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }
}
