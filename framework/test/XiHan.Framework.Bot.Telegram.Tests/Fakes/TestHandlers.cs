// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Telegram.Bot.Types.InlineQueryResults;
using XiHan.Framework.Bot.Telegram.Core;
using XiHan.Framework.Bot.Telegram.Handlers;

namespace XiHan.Framework.Bot.Telegram.Tests.Fakes;

/// <summary>
/// 普通命令处理器（带别名）
/// </summary>
[BotCommand("/order", Description = "下单", Aliases = ["/o"])]
public sealed class TestOrderCommandHandler : IBotCommandHandler
{
    /// <summary>
    /// 处理器名称
    /// </summary>
    public const string HandlerName = "order";

    private readonly HandlerRecorder _recorder;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="recorder">共享记录器</param>
    public TestOrderCommandHandler(HandlerRecorder recorder)
    {
        _recorder = recorder;
    }

    /// <summary>
    /// 处理命令
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="args">命令参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task HandleAsync(TelegramBotContext context, string[] args, CancellationToken cancellationToken = default)
    {
        _recorder.Record(HandlerName, context.Text, args);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 仅管理员可执行的命令处理器
/// </summary>
[BotCommand("/ban", Description = "封禁用户", AdminOnly = true)]
public sealed class TestAdminCommandHandler : IBotCommandHandler
{
    /// <summary>
    /// 处理器名称
    /// </summary>
    public const string HandlerName = "ban";

    private readonly HandlerRecorder _recorder;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="recorder">共享记录器</param>
    public TestAdminCommandHandler(HandlerRecorder recorder)
    {
        _recorder = recorder;
    }

    /// <summary>
    /// 处理命令
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="args">命令参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task HandleAsync(TelegramBotContext context, string[] args, CancellationToken cancellationToken = default)
    {
        _recorder.Record(HandlerName, context.Text, args);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 带正则直达的命令处理器（捕获组作为参数）
/// </summary>
[BotCommand("/query", Description = "查单", Pattern = @"^查单\s+(\d+)$")]
public sealed class TestPatternCommandHandler : IBotCommandHandler
{
    /// <summary>
    /// 处理器名称
    /// </summary>
    public const string HandlerName = "query";

    private readonly HandlerRecorder _recorder;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="recorder">共享记录器</param>
    public TestPatternCommandHandler(HandlerRecorder recorder)
    {
        _recorder = recorder;
    }

    /// <summary>
    /// 处理命令
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="args">命令参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task HandleAsync(TelegramBotContext context, string[] args, CancellationToken cancellationToken = default)
    {
        _recorder.Record(HandlerName, context.Text, args);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 带正则直达但无捕获组的命令处理器
/// </summary>
[BotCommand("/echo", Description = "回声", Pattern = "^重复.*$")]
public sealed class TestNoGroupPatternCommandHandler : IBotCommandHandler
{
    /// <summary>
    /// 处理器名称
    /// </summary>
    public const string HandlerName = "echo";

    private readonly HandlerRecorder _recorder;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="recorder">共享记录器</param>
    public TestNoGroupPatternCommandHandler(HandlerRecorder recorder)
    {
        _recorder = recorder;
    }

    /// <summary>
    /// 处理命令
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="args">命令参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task HandleAsync(TelegramBotContext context, string[] args, CancellationToken cancellationToken = default)
    {
        _recorder.Record(HandlerName, context.Text, args);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 描述过短（不足 3 字符）的命令处理器
/// </summary>
[BotCommand("/ok", Description = "好")]
public sealed class TestShortDescriptionCommandHandler : IBotCommandHandler
{
    /// <summary>
    /// 处理命令
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="args">命令参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task HandleAsync(TelegramBotContext context, string[] args, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// 无描述但带别名的命令处理器
/// </summary>
[BotCommand("/ping", Aliases = ["/pong"])]
public sealed class TestNoDescriptionCommandHandler : IBotCommandHandler
{
    /// <summary>
    /// 处理命令
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="args">命令参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task HandleAsync(TelegramBotContext context, string[] args, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// 与 <see cref="TestOrderCommandHandler"/> 命令冲突的处理器
/// </summary>
[BotCommand("/order")]
public sealed class TestDuplicateOrderCommandHandler : IBotCommandHandler
{
    /// <summary>
    /// 处理命令
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="args">命令参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task HandleAsync(TelegramBotContext context, string[] args, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// 一个类上标注多个命令的处理器
/// </summary>
[BotCommand("/first", Description = "第一个命令")]
[BotCommand("/second", Description = "第二个命令")]
public sealed class TestMultiCommandHandler : IBotCommandHandler
{
    /// <summary>
    /// 处理命令
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="args">命令参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task HandleAsync(TelegramBotContext context, string[] args, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// 缺少 [BotCommand] 属性的命令处理器（注册时应快速失败）
/// </summary>
public sealed class TestCommandHandlerWithoutAttribute : IBotCommandHandler
{
    /// <summary>
    /// 处理命令
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="args">命令参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task HandleAsync(TelegramBotContext context, string[] args, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// 普通回调处理器
/// </summary>
[BotCallback("confirm")]
public sealed class TestConfirmCallbackHandler : IBotCallbackHandler
{
    /// <summary>
    /// 处理器名称
    /// </summary>
    public const string HandlerName = "confirm";

    private readonly HandlerRecorder _recorder;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="recorder">共享记录器</param>
    public TestConfirmCallbackHandler(HandlerRecorder recorder)
    {
        _recorder = recorder;
    }

    /// <summary>
    /// 处理回调
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="data">完整回调数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task HandleAsync(TelegramBotContext context, string data, CancellationToken cancellationToken = default)
    {
        _recorder.Record(HandlerName, data);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 仅管理员可点击的回调处理器
/// </summary>
[BotCallback("purge", AdminOnly = true)]
public sealed class TestAdminCallbackHandler : IBotCallbackHandler
{
    /// <summary>
    /// 处理器名称
    /// </summary>
    public const string HandlerName = "purge";

    private readonly HandlerRecorder _recorder;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="recorder">共享记录器</param>
    public TestAdminCallbackHandler(HandlerRecorder recorder)
    {
        _recorder = recorder;
    }

    /// <summary>
    /// 处理回调
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="data">完整回调数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task HandleAsync(TelegramBotContext context, string data, CancellationToken cancellationToken = default)
    {
        _recorder.Record(HandlerName, data);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 与 <see cref="TestConfirmCallbackHandler"/> 动作冲突的回调处理器
/// </summary>
[BotCallback("confirm")]
public sealed class TestDuplicateConfirmCallbackHandler : IBotCallbackHandler
{
    /// <summary>
    /// 处理回调
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="data">完整回调数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task HandleAsync(TelegramBotContext context, string data, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// 缺少 [BotCallback] 属性的回调处理器（注册时应快速失败）
/// </summary>
public sealed class TestCallbackHandlerWithoutAttribute : IBotCallbackHandler
{
    /// <summary>
    /// 处理回调
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="data">完整回调数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task HandleAsync(TelegramBotContext context, string data, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// 后执行的普通消息处理器（Order 较大）
/// </summary>
public sealed class TestLateMessageHandler : IBotMessageHandler
{
    /// <summary>
    /// 处理器名称
    /// </summary>
    public const string HandlerName = "message-late";

    private readonly HandlerRecorder _recorder;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="recorder">共享记录器</param>
    public TestLateMessageHandler(HandlerRecorder recorder)
    {
        _recorder = recorder;
    }

    /// <summary>
    /// 执行顺序
    /// </summary>
    public int Order => 100;

    /// <summary>
    /// 判断是否处理
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <returns>是否处理</returns>
    public bool CanHandle(TelegramBotContext context)
    {
        return _recorder.MessageCanHandle;
    }

    /// <summary>
    /// 处理消息
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task HandleAsync(TelegramBotContext context, CancellationToken cancellationToken = default)
    {
        _recorder.Record(HandlerName, context.Text);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 先执行的普通消息处理器（Order 较小）
/// </summary>
public sealed class TestEarlyMessageHandler : IBotMessageHandler
{
    /// <summary>
    /// 处理器名称
    /// </summary>
    public const string HandlerName = "message-early";

    private readonly HandlerRecorder _recorder;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="recorder">共享记录器</param>
    public TestEarlyMessageHandler(HandlerRecorder recorder)
    {
        _recorder = recorder;
    }

    /// <summary>
    /// 执行顺序
    /// </summary>
    public int Order => 1;

    /// <summary>
    /// 判断是否处理
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <returns>是否处理</returns>
    public bool CanHandle(TelegramBotContext context)
    {
        return _recorder.MessageCanHandle;
    }

    /// <summary>
    /// 处理消息
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task HandleAsync(TelegramBotContext context, CancellationToken cancellationToken = default)
    {
        _recorder.Record(HandlerName, context.Text);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 从不命中的普通消息处理器
/// </summary>
public sealed class TestNeverMessageHandler : IBotMessageHandler
{
    /// <summary>
    /// 处理器名称
    /// </summary>
    public const string HandlerName = "message-never";

    private readonly HandlerRecorder _recorder;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="recorder">共享记录器</param>
    public TestNeverMessageHandler(HandlerRecorder recorder)
    {
        _recorder = recorder;
    }

    /// <summary>
    /// 执行顺序
    /// </summary>
    public int Order => -10;

    /// <summary>
    /// 判断是否处理
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <returns>是否处理</returns>
    public bool CanHandle(TelegramBotContext context)
    {
        return false;
    }

    /// <summary>
    /// 处理消息
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task HandleAsync(TelegramBotContext context, CancellationToken cancellationToken = default)
    {
        _recorder.Record(HandlerName, context.Text);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 回复消息处理器
/// </summary>
public sealed class TestReplyHandler : IBotReplyHandler
{
    /// <summary>
    /// 处理器名称
    /// </summary>
    public const string HandlerName = "reply";

    private readonly HandlerRecorder _recorder;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="recorder">共享记录器</param>
    public TestReplyHandler(HandlerRecorder recorder)
    {
        _recorder = recorder;
    }

    /// <summary>
    /// 判断是否处理
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <returns>是否处理</returns>
    public bool CanHandle(TelegramBotContext context)
    {
        return _recorder.ReplyCanHandle;
    }

    /// <summary>
    /// 处理回复消息
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task HandleAsync(TelegramBotContext context, CancellationToken cancellationToken = default)
    {
        _recorder.Record(HandlerName, context.Text);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 会话状态处理器
/// </summary>
public sealed class TestStateHandler : IBotStateHandler
{
    /// <summary>
    /// 处理器名称
    /// </summary>
    public const string HandlerName = "state";

    private readonly HandlerRecorder _recorder;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="recorder">共享记录器</param>
    public TestStateHandler(HandlerRecorder recorder)
    {
        _recorder = recorder;
    }

    /// <summary>
    /// 判断是否处理当前步骤
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="stateStep">当前步骤标识</param>
    /// <returns>是否处理</returns>
    public bool CanHandle(TelegramBotContext context, string stateStep)
    {
        return _recorder.StateCanHandle;
    }

    /// <summary>
    /// 处理当前步骤的消息
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="stateStep">当前步骤标识</param>
    /// <param name="statePayload">状态上下文数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task HandleAsync(TelegramBotContext context, string stateStep, string? statePayload, CancellationToken cancellationToken = default)
    {
        _recorder.Record(HandlerName, stateStep, [statePayload ?? string.Empty]);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 内联查询处理器
/// </summary>
public sealed class TestInlineQueryHandler : IBotInlineQueryHandler
{
    /// <summary>
    /// 处理器名称
    /// </summary>
    public const string HandlerName = "inline";

    private readonly HandlerRecorder _recorder;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="recorder">共享记录器</param>
    public TestInlineQueryHandler(HandlerRecorder recorder)
    {
        _recorder = recorder;
    }

    /// <summary>
    /// 判断是否处理该内联查询
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="query">查询文本</param>
    /// <returns>是否处理</returns>
    public bool CanHandle(TelegramBotContext context, string query)
    {
        return _recorder.InlineCanHandle;
    }

    /// <summary>
    /// 处理内联查询
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="query">查询文本</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>内联查询结果列表</returns>
    public Task<IReadOnlyList<InlineQueryResult>> HandleAsync(TelegramBotContext context, string query, CancellationToken cancellationToken = default)
    {
        _recorder.Record(HandlerName, query);
        IReadOnlyList<InlineQueryResult> results = [];
        return Task.FromResult(results);
    }
}

/// <summary>
/// /start 深链参数处理器
/// </summary>
public sealed class TestStartPayloadHandler : IBotStartPayloadHandler
{
    /// <summary>
    /// 处理器名称
    /// </summary>
    public const string HandlerName = "start-payload";

    private readonly HandlerRecorder _recorder;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="recorder">共享记录器</param>
    public TestStartPayloadHandler(HandlerRecorder recorder)
    {
        _recorder = recorder;
    }

    /// <summary>
    /// 处理深链参数
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="payload">深链参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否已消费</returns>
    public Task<bool> HandleAsync(TelegramBotContext context, string payload, CancellationToken cancellationToken = default)
    {
        _recorder.Record(HandlerName, payload);
        return Task.FromResult(_recorder.StartPayloadHandled);
    }
}

/// <summary>
/// 同时实现普通消息与回复消息两条链的处理器
/// </summary>
public sealed class TestMessageAndReplyHandler : IBotMessageHandler, IBotReplyHandler
{
    /// <summary>
    /// 处理器名称
    /// </summary>
    public const string HandlerName = "message-and-reply";

    private readonly HandlerRecorder _recorder;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="recorder">共享记录器</param>
    public TestMessageAndReplyHandler(HandlerRecorder recorder)
    {
        _recorder = recorder;
    }

    /// <summary>
    /// 判断是否处理
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <returns>是否处理</returns>
    public bool CanHandle(TelegramBotContext context)
    {
        return true;
    }

    /// <summary>
    /// 处理消息
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task HandleAsync(TelegramBotContext context, CancellationToken cancellationToken = default)
    {
        _recorder.Record(HandlerName, context.Text);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 未实现任何 IBot*Handler 接口的类型（注册时应快速失败）
/// </summary>
public sealed class TestNotAHandler
{
}
