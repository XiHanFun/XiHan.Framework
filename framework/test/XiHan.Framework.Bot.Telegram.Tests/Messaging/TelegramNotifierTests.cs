// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Bot.Telegram.Messaging;
using XiHan.Framework.Bot.Telegram.MultiBot;
using XiHan.Framework.Bot.Telegram.Tests.Fakes;

namespace XiHan.Framework.Bot.Telegram.Tests.Messaging;

/// <summary>
/// <see cref="TelegramNotifier"/> 发送门面测试
/// </summary>
/// <remarks>
/// 真正的发送要连 Telegram，按外部依赖不实连的原则不在单测覆盖范围内。
/// 这里覆盖发送之前必然执行的两段纯逻辑：
/// 1）入参校验（这些校验在返回 Task 之前同步执行，是调用方最容易踩的边界）；
/// 2）按机器人名称解析实例——解析不到必须抛 KeyNotFoundException 而不是静默假成功，
///    否则机器人被删掉之后消息会消失得无声无息。
/// </remarks>
public class TelegramNotifierTests
{
    /// <summary>
    /// 会话 Id 为 0 时抛参数异常
    /// </summary>
    [Fact]
    public async Task SendTextAsync_WhenChatIdZero_Throws()
    {
        var notifier = CreateNotifier(out _);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await notifier.SendTextAsync("main-bot", 0, "你好", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("chatId", exception.ParamName);
    }

    /// <summary>
    /// 文本为空时抛参数异常
    /// </summary>
    /// <param name="text">文本内容</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task SendTextAsync_WhenTextBlank_Throws(string? text)
    {
        var notifier = CreateNotifier(out _);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await notifier.SendTextAsync("main-bot", 100, text!, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("text", exception.ParamName);
    }

    /// <summary>
    /// Markdown 发送同样执行会话与文本校验
    /// </summary>
    [Fact]
    public async Task SendMarkdownAsync_ValidatesChatIdAndText()
    {
        var notifier = CreateNotifier(out _);

        Assert.Equal("chatId", (await Assert.ThrowsAsync<ArgumentException>(
            async () => await notifier.SendMarkdownAsync("main-bot", 0, "*粗体*", cancellationToken: TestContext.Current.CancellationToken))).ParamName);
        Assert.Equal("text", (await Assert.ThrowsAsync<ArgumentException>(
            async () => await notifier.SendMarkdownAsync("main-bot", 100, "   ", cancellationToken: TestContext.Current.CancellationToken))).ParamName);
    }

    /// <summary>
    /// 按解析模式发送同样执行会话与文本校验
    /// </summary>
    [Fact]
    public async Task SendByParseModeAsync_ValidatesChatIdAndText()
    {
        var notifier = CreateNotifier(out _);

        Assert.Equal("chatId", (await Assert.ThrowsAsync<ArgumentException>(
            async () => await notifier.SendByParseModeAsync("main-bot", 0, "你好", "Html", cancellationToken: TestContext.Current.CancellationToken))).ParamName);
        Assert.Equal("text", (await Assert.ThrowsAsync<ArgumentException>(
            async () => await notifier.SendByParseModeAsync("main-bot", 100, string.Empty, "Html", cancellationToken: TestContext.Current.CancellationToken))).ParamName);
    }

    /// <summary>
    /// 图片字节为空时抛参数异常
    /// </summary>
    [Fact]
    public async Task SendPhotoAsync_WhenImageBytesEmpty_Throws()
    {
        var notifier = CreateNotifier(out _);

        Assert.Equal("imageBytes", (await Assert.ThrowsAsync<ArgumentException>(
            async () => await notifier.SendPhotoAsync("main-bot", 100, [], cancellationToken: TestContext.Current.CancellationToken))).ParamName);
        Assert.Equal("imageBytes", (await Assert.ThrowsAsync<ArgumentException>(
            async () => await notifier.SendPhotoAsync("main-bot", 100, null!, cancellationToken: TestContext.Current.CancellationToken))).ParamName);
    }

    /// <summary>
    /// 图片发送先校验会话 Id
    /// </summary>
    [Fact]
    public async Task SendPhotoAsync_WhenChatIdZero_Throws()
    {
        var notifier = CreateNotifier(out _);

        Assert.Equal("chatId", (await Assert.ThrowsAsync<ArgumentException>(
            async () => await notifier.SendPhotoAsync("main-bot", 0, [1, 2, 3], cancellationToken: TestContext.Current.CancellationToken))).ParamName);
    }

    /// <summary>
    /// 文件字节与文件名为空时抛参数异常
    /// </summary>
    [Fact]
    public async Task SendDocumentAsync_ValidatesBytesAndFileName()
    {
        var notifier = CreateNotifier(out _);

        Assert.Equal("fileBytes", (await Assert.ThrowsAsync<ArgumentException>(
            async () => await notifier.SendDocumentAsync("main-bot", 100, [], "a.txt", cancellationToken: TestContext.Current.CancellationToken))).ParamName);
        Assert.Equal("fileName", (await Assert.ThrowsAsync<ArgumentException>(
            async () => await notifier.SendDocumentAsync("main-bot", 100, [1], "   ", cancellationToken: TestContext.Current.CancellationToken))).ParamName);
    }

    /// <summary>
    /// 编辑消息文本同样执行会话与文本校验
    /// </summary>
    [Fact]
    public async Task EditMessageTextAsync_ValidatesChatIdAndText()
    {
        var notifier = CreateNotifier(out _);

        Assert.Equal("chatId", (await Assert.ThrowsAsync<ArgumentException>(
            async () => await notifier.EditMessageTextAsync("main-bot", 0, 1, "新内容", cancellationToken: TestContext.Current.CancellationToken))).ParamName);
        Assert.Equal("text", (await Assert.ThrowsAsync<ArgumentException>(
            async () => await notifier.EditMessageTextAsync("main-bot", 100, 1, "   ", cancellationToken: TestContext.Current.CancellationToken))).ParamName);
    }

    /// <summary>
    /// 编辑键盘只校验会话 Id（键盘允许为 null，表示移除键盘）
    /// </summary>
    [Fact]
    public async Task EditMessageReplyMarkupAsync_ValidatesChatId()
    {
        var notifier = CreateNotifier(out _);

        Assert.Equal("chatId", (await Assert.ThrowsAsync<ArgumentException>(
            async () => await notifier.EditMessageReplyMarkupAsync("main-bot", 0, 1, cancellationToken: TestContext.Current.CancellationToken))).ParamName);
    }

    /// <summary>
    /// 管理员广播的文本为空时抛参数异常
    /// </summary>
    [Fact]
    public async Task SendToAdminsAsync_WhenTextBlank_Throws()
    {
        var notifier = CreateNotifier(out _);

        Assert.Equal("text", (await Assert.ThrowsAsync<ArgumentException>(
            async () => await notifier.SendToAdminsAsync("main-bot", "   ", cancellationToken: TestContext.Current.CancellationToken))).ParamName);
    }

    /// <summary>
    /// 机器人未注册时抛 KeyNotFoundException，而不是静默丢消息
    /// </summary>
    [Fact]
    public async Task SendTextAsync_WhenBotNotRegistered_ThrowsKeyNotFound()
    {
        var notifier = CreateNotifier(out _);

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            async () => await notifier.SendTextAsync("missing-bot", 100, "你好", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("missing-bot", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 管理员广播时机器人未注册同样抛 KeyNotFoundException
    /// </summary>
    [Fact]
    public async Task SendToAdminsAsync_WhenBotNotRegistered_ThrowsKeyNotFound()
    {
        var notifier = CreateNotifier(out _);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            async () => await notifier.SendToAdminsAsync("missing-bot", "告警", cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 机器人没有配置管理员时广播直接结束，不产生任何发送尝试
    /// </summary>
    [Fact]
    public async Task SendToAdminsAsync_WhenNoAdminConfigured_CompletesWithoutSending()
    {
        var notifier = CreateNotifier(out var registry);
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(name: "main-bot"));
        registry.AddOrUpdate(bot);

        await notifier.SendToAdminsAsync("main-bot", "告警", cancellationToken: TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// 调用方令牌已取消时立即抛取消异常，不做任何发送尝试
    /// </summary>
    [Fact]
    public async Task SendTextAsync_WhenTokenAlreadyCanceled_Throws()
    {
        var notifier = CreateNotifier(out var registry);
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(name: "main-bot"));
        registry.AddOrUpdate(bot);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await notifier.SendTextAsync("main-bot", 100, "你好", cancellationToken: cts.Token));
    }

    /// <summary>
    /// 实现挂在 ITelegramNotifier 抽象上，路由器与处理器只依赖抽象
    /// </summary>
    [Fact]
    public void Type_ImplementsNotifierAbstraction()
    {
        Assert.IsAssignableFrom<ITelegramNotifier>(CreateNotifier(out _));
    }

    /// <summary>
    /// 构造发送门面
    /// </summary>
    /// <param name="registry">机器人注册表</param>
    /// <returns>发送门面</returns>
    private static TelegramNotifier CreateNotifier(out BotRegistry registry)
    {
        registry = new BotRegistry();
        var provider = new ServiceCollection().BuildServiceProvider();
        return new TelegramNotifier(
            registry,
            provider.GetRequiredService<IServiceScopeFactory>(),
            TelegramTestFactory.CreatePlatformOptions(),
            NullLogger<TelegramNotifier>.Instance);
    }
}
