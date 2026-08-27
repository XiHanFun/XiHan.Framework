// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Enums;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.Lark.Messaging;
using XiHan.Framework.Bot.Lark.Options;
using XiHan.Framework.Bot.Lark.Tests.Fakes;

namespace XiHan.Framework.Bot.Lark.Tests.Messaging;

/// <summary>
/// 飞书 Bot 提供者测试
/// </summary>
/// <remarks>
/// 提供者在真正构造 LarkBot 之前有三道短路：配置为空、提供者被禁用、访问令牌缺失。
/// 这三条是唯一不出网的分支，也是线上最容易踩的配置错误，逐条覆盖；
/// 令牌合法之后的分支必然发起 HTTP，CI 不允许出网，见文末 Skip 用例。
/// 配置存储用手写 fake，顺带验证「每次发送都重新取配置」与「取消令牌被透传给存储」。
/// </remarks>
public class LarkBotProviderTests
{
    /// <summary>
    /// 提供者名称与常量表一致
    /// </summary>
    /// <remarks>
    /// 名称是渠道路由的匹配键，先对字面量再对常量，防止常量和实现各改一半。
    /// </remarks>
    [Fact]
    public void Name_Always_IsLarkProviderName()
    {
        var provider = new LarkBotProvider(new FakeLarkConfigStore(new LarkOptions()));

        Assert.Equal("Lark", provider.Name);
        Assert.Equal(BotProviderNames.Lark, provider.Name);
    }

    /// <summary>
    /// 提供者实现统一抽象
    /// </summary>
    [Fact]
    public void Provider_Always_ImplementsBotProviderAbstraction()
    {
        Assert.True(typeof(LarkBotProvider).IsAssignableTo(typeof(IBotProvider)));
    }

    /// <summary>
    /// 配置存储返回 null 时按未配置短路
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenConfigStoreReturnsNull_ReturnsBadRequest()
    {
        var store = new FakeLarkConfigStore(null);
        var provider = new LarkBotProvider(store);

        var result = await provider.SendAsync(CreateMessage(), CreateContext());

        AssertBadRequest(result, "Lark provider is not configured or disabled.");
        Assert.Equal(1, store.GetCallCount);
    }

    /// <summary>
    /// 提供者被禁用时短路，即使访问令牌完整也不发送
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenProviderDisabled_ReturnsBadRequest()
    {
        var store = new FakeLarkConfigStore(new LarkOptions
        {
            Enabled = false,
            AccessToken = "abc-token"
        });
        var provider = new LarkBotProvider(store);

        var result = await provider.SendAsync(CreateMessage(), CreateContext());

        AssertBadRequest(result, "Lark provider is not configured or disabled.");
    }

    /// <summary>
    /// 访问令牌为空白时短路
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task SendAsync_WhenAccessTokenBlank_ReturnsBadRequest(string accessToken)
    {
        var store = new FakeLarkConfigStore(new LarkOptions
        {
            Enabled = true,
            AccessToken = accessToken
        });
        var provider = new LarkBotProvider(store);

        var result = await provider.SendAsync(CreateMessage(), CreateContext());

        AssertBadRequest(result, "Lark access token is required.");
    }

    /// <summary>
    /// 两类短路返回不同的错误消息，便于定位配置问题
    /// </summary>
    [Fact]
    public async Task SendAsync_ShortCircuitReasons_AreDistinguishable()
    {
        var disabled = await new LarkBotProvider(new FakeLarkConfigStore(new LarkOptions { Enabled = false }))
            .SendAsync(CreateMessage(), CreateContext());
        var missingToken = await new LarkBotProvider(new FakeLarkConfigStore(new LarkOptions { Enabled = true }))
            .SendAsync(CreateMessage(), CreateContext());

        Assert.NotEqual(disabled.Message, missingToken.Message);
    }

    /// <summary>
    /// 短路结果同样带上提供者名称
    /// </summary>
    /// <remarks>
    /// BotContext.AddResult 会用提供者名补齐，但短路结果是直接返回给调度器的，
    /// 少了 Provider 会让多渠道汇总时分不清是谁失败的。
    /// </remarks>
    [Fact]
    public async Task SendAsync_WhenShortCircuited_TagsResultWithProviderName()
    {
        var provider = new LarkBotProvider(new FakeLarkConfigStore(null));

        var result = await provider.SendAsync(CreateMessage(), CreateContext());

        Assert.Equal(BotProviderNames.Lark, result.Provider);
    }

    /// <summary>
    /// 上下文的取消令牌透传给配置存储
    /// </summary>
    [Fact]
    public async Task SendAsync_Always_PassesContextCancellationTokenToConfigStore()
    {
        using var cts = new CancellationTokenSource();
        var store = new FakeLarkConfigStore(null);
        var provider = new LarkBotProvider(store);
        var context = new BotContext(CreateMessage(), [BotProviderNames.Lark], cts.Token);

        await provider.SendAsync(CreateMessage(), context);

        Assert.Equal(cts.Token, store.LastCancellationToken);
    }

    /// <summary>
    /// 未配置时任何消息类型都走同一条短路
    /// </summary>
    /// <remarks>
    /// 校验短路顺序：先判配置再看消息类型，避免为了取扩展数据先做无谓的类型转换。
    /// </remarks>
    [Theory]
    [InlineData(BotMessageType.Text)]
    [InlineData(BotMessageType.Markdown)]
    [InlineData(BotMessageType.Card)]
    [InlineData(BotMessageType.Image)]
    [InlineData(BotMessageType.File)]
    [InlineData(BotMessageType.Link)]
    public async Task SendAsync_WhenNotConfigured_ShortCircuitsForEveryMessageType(BotMessageType messageType)
    {
        var message = CreateMessage();
        message.Type = messageType;
        var provider = new LarkBotProvider(new FakeLarkConfigStore(null));

        var result = await provider.SendAsync(message, CreateContext());

        AssertBadRequest(result, "Lark provider is not configured or disabled.");
    }

    /// <summary>
    /// 每次发送都重新读取一次配置
    /// </summary>
    /// <remarks>
    /// 配置存储允许被应用层换成数据库实现并热更新，提供者不能把配置缓存在字段里。
    /// </remarks>
    [Fact]
    public async Task SendAsync_CalledTwice_ReadsConfigStoreEachTime()
    {
        var store = new FakeLarkConfigStore(null);
        var provider = new LarkBotProvider(store);

        await provider.SendAsync(CreateMessage(), CreateContext());
        await provider.SendAsync(CreateMessage(), CreateContext());

        Assert.Equal(2, store.GetCallCount);
    }

    /// <summary>
    /// 令牌合法后的真实发送链路不在单元测试覆盖范围
    /// </summary>
    [Fact]
    public void SendAsync_WithValidToken_RequiresCredentials()
    {
        Assert.Skip("访问令牌合法后 LarkBotProvider 会真的向 open.feishu.cn 发起 HTTP，需要真实凭据与外网，CI 不具备");
    }

    /// <summary>
    /// 构造一条最简文本消息
    /// </summary>
    private static BotMessage CreateMessage()
    {
        return new BotMessage
        {
            Content = "hello"
        };
    }

    /// <summary>
    /// 构造调度上下文
    /// </summary>
    private static BotContext CreateContext()
    {
        return new BotContext(CreateMessage(), [BotProviderNames.Lark], TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// 断言短路结果的状态码、消息与提供者名
    /// </summary>
    private static void AssertBadRequest(BotResult result, string expectedMessage)
    {
        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Equal(BotResultCodes.BadRequest, result.Code);
        Assert.Equal(expectedMessage, result.Message);
        Assert.Equal(BotProviderNames.Lark, result.Provider);
        Assert.Null(result.Data);
    }
}
