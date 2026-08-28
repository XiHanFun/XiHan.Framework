// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using XiHan.Framework.Bot.Telegram.Core;
using XiHan.Framework.Bot.Telegram.MultiBot;
using XiHan.Framework.Bot.Telegram.Options;
using XiHan.Framework.Bot.Telegram.Routing;

namespace XiHan.Framework.Bot.Telegram.Tests.Fakes;

/// <summary>
/// Telegram 测试对象工厂
/// </summary>
/// <remarks>
/// 统一构造合法的机器人配置与 Update 对象：
/// 1）Token 必须是 <c>数字:密钥</c> 形式，Telegram.Bot 在客户端构造阶段就会解析 BotId，格式不对直接抛异常；
/// 2）CallbackQuery.Id 默认留空——路由器只在回调 Id 非空时才调用 AnswerCallbackQuery，
///    留空即可让全部用例在零网络请求的前提下跑完回调链路。
/// </remarks>
internal static class TelegramTestFactory
{
    /// <summary>
    /// 合法格式的测试 Token
    /// </summary>
    public const string ValidToken = "123456:AAHfake-telegram-token";

    /// <summary>
    /// 默认测试机器人名称
    /// </summary>
    public const string BotName = "test-bot";

    /// <summary>
    /// 构造机器人配置
    /// </summary>
    /// <param name="name">机器人名称</param>
    /// <param name="adminUsers">管理员用户 Id 列表</param>
    /// <param name="allowedGroupChatIds">群组白名单</param>
    /// <param name="allowedCommands">命令白名单</param>
    /// <param name="enableFallbackReply">是否启用兜底回复</param>
    /// <returns>机器人配置</returns>
    public static TelegramBotConfig CreateConfig(
        string name = BotName,
        long[]? adminUsers = null,
        long[]? allowedGroupChatIds = null,
        string[]? allowedCommands = null,
        bool enableFallbackReply = false)
    {
        return new TelegramBotConfig
        {
            Id = 1,
            Name = name,
            Token = ValidToken,
            AdminUsers = adminUsers ?? [],
            AllowedGroupChatIds = allowedGroupChatIds ?? [],
            AllowedCommands = allowedCommands ?? [],
            EnableFallbackReply = enableFallbackReply
        };
    }

    /// <summary>
    /// 构造机器人运行实例
    /// </summary>
    /// <param name="config">机器人配置</param>
    /// <returns>机器人运行实例</returns>
    public static BotInstance CreateBot(TelegramBotConfig? config = null)
    {
        return new BotInstance(config ?? CreateConfig());
    }

    /// <summary>
    /// 构造一条消息
    /// </summary>
    /// <param name="chatId">会话 Id</param>
    /// <param name="userId">用户 Id</param>
    /// <param name="text">文本内容</param>
    /// <param name="chatType">会话类型</param>
    /// <param name="messageId">消息 Id</param>
    /// <param name="languageCode">用户语言代码</param>
    /// <returns>消息</returns>
    public static Message CreateMessage(
        long chatId = 100,
        long userId = 200,
        string? text = null,
        ChatType chatType = ChatType.Private,
        int messageId = 11,
        string? languageCode = null)
    {
        return new Message
        {
            Id = messageId,
            Chat = new Chat { Id = chatId, Type = chatType },
            From = new User { Id = userId, IsBot = false, FirstName = "tester", LanguageCode = languageCode },
            Text = text
        };
    }

    /// <summary>
    /// 构造一条消息型 Update
    /// </summary>
    /// <param name="text">文本内容</param>
    /// <param name="chatId">会话 Id</param>
    /// <param name="userId">用户 Id</param>
    /// <param name="chatType">会话类型</param>
    /// <param name="updateId">Update Id</param>
    /// <param name="messageId">消息 Id</param>
    /// <returns>Update</returns>
    public static Update CreateMessageUpdate(
        string? text = "hello",
        long chatId = 100,
        long userId = 200,
        ChatType chatType = ChatType.Private,
        int updateId = 1,
        int messageId = 11)
    {
        return new Update
        {
            Id = updateId,
            Message = CreateMessage(chatId, userId, text, chatType, messageId)
        };
    }

    /// <summary>
    /// 构造一条回调型 Update（回调 Id 默认留空，避免触发真实的 AnswerCallbackQuery 请求）
    /// </summary>
    /// <param name="data">回调数据</param>
    /// <param name="chatId">会话 Id</param>
    /// <param name="userId">用户 Id</param>
    /// <param name="callbackId">回调 Id</param>
    /// <param name="chatType">会话类型</param>
    /// <param name="updateId">Update Id</param>
    /// <returns>Update</returns>
    public static Update CreateCallbackUpdate(
        string? data,
        long chatId = 100,
        long userId = 200,
        string callbackId = "",
        ChatType chatType = ChatType.Private,
        int updateId = 2)
    {
        return new Update
        {
            Id = updateId,
            CallbackQuery = new CallbackQuery
            {
                Id = callbackId,
                Data = data,
                From = new User { Id = userId, IsBot = false, FirstName = "tester" },
                Message = CreateMessage(chatId, userId, "原始消息", chatType)
            }
        };
    }

    /// <summary>
    /// 构造一条内联查询型 Update
    /// </summary>
    /// <param name="query">查询文本</param>
    /// <param name="userId">用户 Id</param>
    /// <param name="inlineQueryId">内联查询 Id</param>
    /// <param name="updateId">Update Id</param>
    /// <returns>Update</returns>
    public static Update CreateInlineQueryUpdate(
        string? query,
        long userId = 200,
        string inlineQueryId = "iq-1",
        int updateId = 3)
    {
        return new Update
        {
            Id = updateId,
            InlineQuery = new InlineQuery
            {
                Id = inlineQueryId,
                // 本工厂刻意允许传 null 查询文本，用于覆盖分发器对空查询的处理分支；
                // InlineQuery.Query 声明为非空，这里用 null! 表明是测试有意为之。
                Query = query!,
                Offset = string.Empty,
                From = new User { Id = userId, IsBot = false, FirstName = "tester" }
            }
        };
    }

    /// <summary>
    /// 构造一条既无消息也无回调的空 Update（ChatId / UserId 均为 0）
    /// </summary>
    /// <param name="updateId">Update Id</param>
    /// <returns>Update</returns>
    public static Update CreateEmptyUpdate(int updateId = 5)
    {
        return new Update { Id = updateId };
    }

    /// <summary>
    /// 构造更新上下文
    /// </summary>
    /// <param name="bot">机器人实例</param>
    /// <param name="update">Telegram Update</param>
    /// <returns>更新上下文</returns>
    public static TelegramBotContext CreateContext(BotInstance bot, Update update)
    {
        return new TelegramBotContext(bot, update);
    }

    /// <summary>
    /// 按显式登记的处理器类型构造处理器目录
    /// </summary>
    /// <param name="handlerTypes">处理器类型</param>
    /// <returns>处理器目录</returns>
    public static TelegramBotHandlerCatalog CreateCatalog(params Type[] handlerTypes)
    {
        var options = new TelegramBotHandlerOptions();
        foreach (var handlerType in handlerTypes)
        {
            options.Handlers.Add(handlerType);
        }

        return new TelegramBotHandlerCatalog(Microsoft.Extensions.Options.Options.Create(options));
    }

    /// <summary>
    /// 构造平台选项监视器
    /// </summary>
    /// <param name="configure">选项配置委托</param>
    /// <returns>平台选项监视器</returns>
    public static TestOptionsMonitor<TelegramBotPlatformOptions> CreatePlatformOptions(
        Action<TelegramBotPlatformOptions>? configure = null)
    {
        var options = new TelegramBotPlatformOptions();
        configure?.Invoke(options);
        return new TestOptionsMonitor<TelegramBotPlatformOptions>(options);
    }

    /// <summary>
    /// 构造带处理器注册的作用域服务提供者
    /// </summary>
    /// <remarks>
    /// 路由器统一通过 <c>provider.GetService(handlerType)</c> 解析处理器，
    /// 故意不注册某个类型即可复现「已登记目录但未注册 DI」的降级分支。
    /// </remarks>
    /// <param name="recorder">共享记录器</param>
    /// <param name="handlerTypes">要注册进 DI 的处理器类型</param>
    /// <returns>服务提供者</returns>
    public static ServiceProvider CreateHandlerProvider(HandlerRecorder recorder, params Type[] handlerTypes)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(recorder);
        foreach (var handlerType in handlerTypes)
        {
            _ = services.AddTransient(handlerType);
        }

        return services.BuildServiceProvider();
    }
}
