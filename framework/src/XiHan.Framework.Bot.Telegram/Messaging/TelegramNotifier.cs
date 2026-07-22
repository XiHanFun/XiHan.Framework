// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using XiHan.Framework.Bot.Telegram.Abstractions;
using XiHan.Framework.Bot.Telegram.MultiBot;
using XiHan.Framework.Bot.Telegram.Options;

namespace XiHan.Framework.Bot.Telegram.Messaging;

/// <summary>
/// Telegram 主动发送门面默认实现
/// </summary>
/// <remarks>
/// 全部发送内建重试环：429 读 RetryAfter 精确等待，5xx/网络异常/超时按指数退避；
/// 最终失败按重试配置可通知管理员；每次发送结果写出站审计存储（审计失败不影响主流程）。
/// </remarks>
public sealed class TelegramNotifier : ITelegramNotifier
{
    private readonly BotRegistry _registry;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<TelegramBotPlatformOptions> _options;
    private readonly ILogger<TelegramNotifier> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="registry">机器人注册表</param>
    /// <param name="scopeFactory">服务作用域工厂</param>
    /// <param name="options">平台选项监视器</param>
    /// <param name="logger">日志记录器</param>
    public TelegramNotifier(
        BotRegistry registry,
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<TelegramBotPlatformOptions> options,
        ILogger<TelegramNotifier> logger)
    {
        _registry = registry;
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Message> SendTextAsync(string botName, long chatId, string text, int? replyToMessageId = null, ReplyMarkup? replyMarkup = null, CancellationToken cancellationToken = default)
    {
        ValidateChatId(chatId);
        ValidateText(text);
        return SendMessageCoreAsync(botName, chatId, text, parseMode: null, replyToMessageId, replyMarkup, notifyAdminsOnFailure: true, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Message> SendMarkdownAsync(string botName, long chatId, string markdownText, int? replyToMessageId = null, ReplyMarkup? replyMarkup = null, CancellationToken cancellationToken = default)
    {
        ValidateChatId(chatId);
        ValidateText(markdownText);
        return SendMessageCoreAsync(botName, chatId, markdownText, ParseMode.Markdown, replyToMessageId, replyMarkup, notifyAdminsOnFailure: true, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Message> SendByParseModeAsync(string botName, long chatId, string text, string? parseMode, int? replyToMessageId = null, ReplyMarkup? replyMarkup = null, CancellationToken cancellationToken = default)
    {
        ValidateChatId(chatId);
        ValidateText(text);
        return SendMessageCoreAsync(botName, chatId, text, ResolveParseMode(parseMode), replyToMessageId, replyMarkup, notifyAdminsOnFailure: true, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Message> SendPhotoAsync(string botName, long chatId, byte[] imageBytes, string? caption = null, int? replyToMessageId = null, ReplyMarkup? replyMarkup = null, CancellationToken cancellationToken = default)
    {
        ValidateChatId(chatId);
        if (imageBytes is not { Length: > 0 })
        {
            throw new ArgumentException("imageBytes 不能为空。", nameof(imageBytes));
        }

        return ExecuteWithRetryAsync(
            botName,
            chatId,
            apiMethod: "sendPhoto",
            messageType: "photo",
            content: caption,
            parseMode: null,
            action: async (bot, ct) =>
            {
                var replyParameters = BuildReplyParameters(replyToMessageId);
                await using var stream = new MemoryStream(imageBytes, writable: false);
                var inputFile = InputFile.FromStream(stream, "photo.png");
                return await bot.Client.SendPhoto(
                    chatId: chatId,
                    photo: inputFile,
                    caption: caption,
                    replyParameters: replyParameters,
                    replyMarkup: replyMarkup,
                    cancellationToken: ct);
            },
            notifyAdminsOnFailure: true,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<Message> SendDocumentAsync(string botName, long chatId, byte[] fileBytes, string fileName, string? caption = null, int? replyToMessageId = null, ReplyMarkup? replyMarkup = null, CancellationToken cancellationToken = default)
    {
        ValidateChatId(chatId);
        if (fileBytes is not { Length: > 0 })
        {
            throw new ArgumentException("fileBytes 不能为空。", nameof(fileBytes));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("fileName 不能为空。", nameof(fileName));
        }

        return ExecuteWithRetryAsync(
            botName,
            chatId,
            apiMethod: "sendDocument",
            messageType: "document",
            content: caption ?? fileName,
            parseMode: null,
            action: async (bot, ct) =>
            {
                var replyParameters = BuildReplyParameters(replyToMessageId);
                await using var stream = new MemoryStream(fileBytes, writable: false);
                var inputFile = InputFile.FromStream(stream, fileName);
                return await bot.Client.SendDocument(
                    chatId: chatId,
                    document: inputFile,
                    caption: caption,
                    replyParameters: replyParameters,
                    replyMarkup: replyMarkup,
                    cancellationToken: ct);
            },
            notifyAdminsOnFailure: true,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<Message> EditMessageTextAsync(string botName, long chatId, int messageId, string text, string? parseMode = null, InlineKeyboardMarkup? replyMarkup = null, CancellationToken cancellationToken = default)
    {
        ValidateChatId(chatId);
        ValidateText(text);
        var mode = ResolveParseMode(parseMode);

        return ExecuteWithRetryAsync(
            botName,
            chatId,
            apiMethod: "editMessageText",
            messageType: "text",
            content: text,
            parseMode: mode,
            action: (bot, ct) => bot.Client.EditMessageText(
                chatId: chatId,
                messageId: messageId,
                text: text,
                parseMode: mode ?? ParseMode.None,
                replyMarkup: replyMarkup,
                cancellationToken: ct),
            notifyAdminsOnFailure: true,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<Message> EditMessageReplyMarkupAsync(string botName, long chatId, int messageId, InlineKeyboardMarkup? replyMarkup = null, CancellationToken cancellationToken = default)
    {
        ValidateChatId(chatId);

        return ExecuteWithRetryAsync(
            botName,
            chatId,
            apiMethod: "editMessageReplyMarkup",
            messageType: "reply_markup",
            content: null,
            parseMode: null,
            action: (bot, ct) => bot.Client.EditMessageReplyMarkup(
                chatId: chatId,
                messageId: messageId,
                replyMarkup: replyMarkup,
                cancellationToken: ct),
            notifyAdminsOnFailure: true,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendToAdminsAsync(string botName, string text, string? parseMode = null, CancellationToken cancellationToken = default)
    {
        ValidateText(text);

        var bot = _registry.GetRequired(botName);
        var mode = ResolveParseMode(parseMode);
        var adminIds = (bot.Config.AdminUsers ?? []).Where(x => x > 0).Distinct().ToList();
        foreach (var adminId in adminIds)
        {
            try
            {
                _ = await SendMessageCoreAsync(botName, adminId, text, mode, replyToMessageId: null, replyMarkup: null, notifyAdminsOnFailure: false, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Telegram 管理员通知发送失败。Bot={BotName}, UserId={UserId}", botName, adminId);
            }
        }
    }

    private static void ValidateChatId(long chatId)
    {
        if (chatId == 0)
        {
            throw new ArgumentException("chatId 不能为空。", nameof(chatId));
        }
    }

    private static void ValidateText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("text 不能为空。", nameof(text));
        }
    }

    private static ReplyParameters? BuildReplyParameters(int? replyToMessageId)
    {
        return replyToMessageId.HasValue
            ? new ReplyParameters { MessageId = replyToMessageId.Value }
            : null;
    }

    private static bool IsRetryable(Exception ex)
    {
        if (ex is OperationCanceledException)
        {
            return false;
        }

        // 注意判定顺序：ApiRequestException 派生自 RequestException，须先按错误码精确判定
        if (ex is ApiRequestException apiEx)
        {
            return apiEx.ErrorCode == 429 || apiEx.ErrorCode is >= 500 and < 600;
        }

        // Telegram.Bot 22.x 把超时/网络异常/响应反序列化失败统一包装为非 API 的 RequestException
        if (ex is RequestException)
        {
            return true;
        }

        return ex is HttpRequestException or TimeoutException;
    }

    private static TimeSpan GetRetryDelay(Exception ex, int attempt, TelegramBotRetryOptions options)
    {
        if (ex is ApiRequestException { ErrorCode: 429 } apiEx)
        {
            var retryAfter = apiEx.Parameters?.RetryAfter;
            if (retryAfter > 0)
            {
                return TimeSpan.FromSeconds(retryAfter.Value);
            }
        }

        // 指数退避用 long 运算并钳制移位次数，避免大 attempt 下 int 溢出为负值
        var shift = Math.Min(attempt - 1, 20);
        var delayMs = Math.Min(options.MaxDelayMs, options.BaseDelayMs * (1L << shift));
        return TimeSpan.FromMilliseconds(Math.Max(1, delayMs));
    }

    private ParseMode? ResolveParseMode(string? parseMode)
    {
        if (string.IsNullOrWhiteSpace(parseMode))
        {
            return null;
        }

        var value = parseMode.Trim();
        if (value.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (value.Equals("markdown", StringComparison.OrdinalIgnoreCase))
        {
            return ParseMode.Markdown;
        }

        if (value.Equals("markdownv2", StringComparison.OrdinalIgnoreCase))
        {
            return ParseMode.MarkdownV2;
        }

        if (value.Equals("html", StringComparison.OrdinalIgnoreCase))
        {
            return ParseMode.Html;
        }

        _logger.LogWarning("未识别的解析模式，已按纯文本发送。ParseMode={ParseMode}", parseMode);
        return null;
    }

    private Task<Message> SendMessageCoreAsync(
        string botName,
        long chatId,
        string text,
        ParseMode? parseMode,
        int? replyToMessageId,
        ReplyMarkup? replyMarkup,
        bool notifyAdminsOnFailure,
        CancellationToken cancellationToken)
    {
        return ExecuteWithRetryAsync(
            botName,
            chatId,
            apiMethod: "sendMessage",
            messageType: "text",
            content: text,
            parseMode: parseMode,
            action: (bot, ct) =>
            {
                var replyParameters = BuildReplyParameters(replyToMessageId);
                return parseMode.HasValue
                    ? bot.Client.SendMessage(
                        chatId: chatId,
                        text: text,
                        parseMode: parseMode.Value,
                        replyParameters: replyParameters,
                        replyMarkup: replyMarkup,
                        cancellationToken: ct)
                    : bot.Client.SendMessage(
                        chatId: chatId,
                        text: text,
                        replyParameters: replyParameters,
                        replyMarkup: replyMarkup,
                        cancellationToken: ct);
            },
            notifyAdminsOnFailure,
            cancellationToken);
    }

    private async Task<Message> ExecuteWithRetryAsync(
        string botName,
        long chatId,
        string apiMethod,
        string messageType,
        string? content,
        ParseMode? parseMode,
        Func<BotInstance, CancellationToken, Task<Message>> action,
        bool notifyAdminsOnFailure,
        CancellationToken cancellationToken)
    {
        var retryOptions = _options.CurrentValue.Retry;
        var sw = Stopwatch.StartNew();
        var attempt = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 每次尝试重新解析实例：机器人配置变更被重建后自动切换到新实例（旧实例延迟释放期间不再复用）；
            // 机器人被删除则抛 KeyNotFoundException，属正确失败
            var bot = _registry.GetRequired(botName);

            try
            {
                var message = await action(bot, cancellationToken);
                await TryAuditAsync(bot, chatId, apiMethod, messageType, content, parseMode, message, exception: null, sw.ElapsedMilliseconds, cancellationToken);
                return message;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // 仅调用方取消时短路；底层裸抛的取消异常（如客户端关停）落入下方分支，保证审计与告警
                throw;
            }
            catch (Exception ex) when (IsRetryable(ex) && attempt < retryOptions.MaxRetries)
            {
                attempt++;
                var delay = GetRetryDelay(ex, attempt, retryOptions);
                _logger.LogWarning(
                    "Telegram {ApiMethod} 失败，第 {Attempt}/{MaxRetries} 次重试，等待 {DelayMs:F0}ms。Bot={BotName}, ChatId={ChatId}",
                    apiMethod, attempt, retryOptions.MaxRetries, delay.TotalMilliseconds, botName, chatId);
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                await TryAuditAsync(bot, chatId, apiMethod, messageType, content, parseMode, message: null, exception: ex, sw.ElapsedMilliseconds, cancellationToken);

                if (notifyAdminsOnFailure && retryOptions.NotifyAdminOnFinalFailure)
                {
                    await TryNotifyAdminsOnFailureAsync(botName, chatId, apiMethod, ex, cancellationToken);
                }

                throw;
            }
        }
    }

    private async Task TryAuditAsync(
        BotInstance bot,
        long chatId,
        string apiMethod,
        string messageType,
        string? content,
        ParseMode? parseMode,
        Message? message,
        Exception? exception,
        long elapsedMs,
        CancellationToken cancellationToken)
    {
        try
        {
            var record = new TelegramMessageAuditRecord
            {
                BotName = bot.Name,
                BotConfigId = bot.Config.Id,
                ChatId = chatId,
                ApiMethod = apiMethod,
                MessageType = messageType,
                Content = content,
                ParseMode = parseMode?.ToString() ?? "None",
                TelegramMessageId = message?.MessageId,
                Success = exception is null,
                ErrorCode = (exception as ApiRequestException)?.ErrorCode,
                ErrorMessage = exception?.Message,
                ElapsedMs = elapsedMs,
                SendTime = DateTimeOffset.UtcNow
            };

            using var scope = _scopeFactory.CreateScope();
            var auditStore = scope.ServiceProvider.GetRequiredService<ITelegramMessageAuditStore>();
            await auditStore.AppendAsync(record, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram 出站消息审计失败。Bot={BotName}, ApiMethod={ApiMethod}", bot.Name, apiMethod);
        }
    }

    private async Task TryNotifyAdminsOnFailureAsync(string botName, long chatId, string apiMethod, Exception exception, CancellationToken cancellationToken)
    {
        try
        {
            var title = _options.CurrentValue.Texts.SendFailureAdminNotifyTitle;
            _logger.LogError(exception, "Telegram 消息发送最终失败。Bot={BotName}, ChatId={ChatId}, Api={ApiMethod}", botName, chatId, apiMethod);
            await SendToAdminsAsync(
                botName,
                $"{title}\nBot: {botName}\nChatId: {chatId}\nApi: {apiMethod}\nError: {exception.Message}",
                parseMode: null,
                cancellationToken);
        }
        catch
        {
            // 通知失败不抛异常，避免覆盖原始错误
        }
    }
}
