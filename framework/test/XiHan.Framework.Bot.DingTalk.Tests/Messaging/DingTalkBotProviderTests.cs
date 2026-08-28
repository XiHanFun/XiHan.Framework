// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.DingTalk.Messaging;
using XiHan.Framework.Bot.DingTalk.Options;
using XiHan.Framework.Bot.DingTalk.Tests.Fakes;
using XiHan.Framework.Bot.Enums;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Providers;

namespace XiHan.Framework.Bot.DingTalk.Tests.Messaging;

/// <summary>
/// 钉钉 Bot 提供者测试
/// </summary>
/// <remarks>
/// 只覆盖"发请求之前"的守卫分支：未配置、已停用、缺访问令牌。
/// 这三条分支是提供者真正自有的编排逻辑，且都在构造 DingTalkBot 之前就短路返回，可以做到零网络。
/// 一旦配置齐全，后续动作就是对 oapi.dingtalk.com 的真实 POST，本仓测试铁律禁止实连，
/// 因此消息类型路由与 @ 人构造不在这里覆盖（见交付报告的未覆盖说明）。
/// </remarks>
public class DingTalkBotProviderTests
{
    /// <summary>
    /// 提供者名称与框架常量一致
    /// </summary>
    /// <remarks>
    /// 渠道配置里按字符串匹配提供者，名称漂移会让整条渠道静默失效。
    /// </remarks>
    [Fact]
    public void Name_MatchesFrameworkProviderName()
    {
        var provider = new DingTalkBotProvider(new FakeDingTalkConfigStore(null));

        Assert.Equal(BotProviderNames.DingTalk, provider.Name);
        Assert.Equal("DingTalk", provider.Name);
    }

    /// <summary>
    /// 提供者实现框架的提供者抽象
    /// </summary>
    [Fact]
    public void Provider_ImplementsBotProviderAbstraction()
    {
        Assert.True(typeof(DingTalkBotProvider).IsAssignableTo(typeof(IBotProvider)));
    }

    /// <summary>
    /// 配置缺失时直接返回请求错误，不触发任何发送
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenConfigMissing_ReturnsBadRequest()
    {
        var store = new FakeDingTalkConfigStore(null);
        var provider = new DingTalkBotProvider(store);
        var message = new BotMessage { Content = "构建失败" };
        var context = new BotContext(message, [], TestContext.Current.CancellationToken);

        var result = await provider.SendAsync(message, context);

        Assert.False(result.IsSuccess);
        Assert.Equal(BotResultCodes.BadRequest, result.Code);
        Assert.Equal("DingTalk provider is not configured or disabled.", result.Message);
        Assert.Equal(BotProviderNames.DingTalk, result.Provider);
        Assert.Equal(1, store.GetCallCount);
    }

    /// <summary>
    /// 提供者被停用时即使令牌齐全也不发送
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenDisabled_ReturnsBadRequestEvenWithAccessToken()
    {
        var store = new FakeDingTalkConfigStore(new DingTalkOptions
        {
            Enabled = false,
            AccessToken = "access-token-value",
            Secret = "SECsecretvalue"
        });
        var provider = new DingTalkBotProvider(store);
        var message = new BotMessage { Content = "构建失败" };
        var context = new BotContext(message, [], TestContext.Current.CancellationToken);

        var result = await provider.SendAsync(message, context);

        Assert.False(result.IsSuccess);
        Assert.Equal(BotResultCodes.BadRequest, result.Code);
        Assert.Equal("DingTalk provider is not configured or disabled.", result.Message);
        Assert.Equal(BotProviderNames.DingTalk, result.Provider);
    }

    /// <summary>
    /// 访问令牌为空或全空白时返回令牌缺失错误
    /// </summary>
    /// <param name="accessToken">访问令牌</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task SendAsync_WhenAccessTokenBlank_ReturnsTokenRequired(string accessToken)
    {
        var store = new FakeDingTalkConfigStore(new DingTalkOptions
        {
            Enabled = true,
            AccessToken = accessToken,
            Secret = "SECsecretvalue"
        });
        var provider = new DingTalkBotProvider(store);
        var message = new BotMessage { Content = "构建失败" };
        var context = new BotContext(message, [], TestContext.Current.CancellationToken);

        var result = await provider.SendAsync(message, context);

        Assert.False(result.IsSuccess);
        Assert.Equal(BotResultCodes.BadRequest, result.Code);
        Assert.Equal("DingTalk access token is required.", result.Message);
        Assert.Equal(BotProviderNames.DingTalk, result.Provider);
    }

    /// <summary>
    /// 停用判定优先于令牌判定
    /// </summary>
    /// <remarks>
    /// 两个守卫的顺序决定了排障时看到哪句话：停用的渠道不该报"缺令牌"，否则会把人引向配置令牌的死胡同。
    /// </remarks>
    [Fact]
    public async Task SendAsync_WhenDisabledAndTokenBlank_ReportsDisabledFirst()
    {
        var store = new FakeDingTalkConfigStore(new DingTalkOptions
        {
            Enabled = false,
            AccessToken = string.Empty
        });
        var provider = new DingTalkBotProvider(store);
        var message = new BotMessage { Content = "构建失败" };
        var context = new BotContext(message, [], TestContext.Current.CancellationToken);

        var result = await provider.SendAsync(message, context);

        Assert.Equal("DingTalk provider is not configured or disabled.", result.Message);
    }

    /// <summary>
    /// 上下文的取消令牌被原样透传给配置存储
    /// </summary>
    [Fact]
    public async Task SendAsync_PassesContextCancellationTokenToConfigStore()
    {
        using var cancellation = new CancellationTokenSource();
        var store = new FakeDingTalkConfigStore(null);
        var provider = new DingTalkBotProvider(store);
        var message = new BotMessage { Content = "构建失败" };
        var context = new BotContext(message, [], cancellation.Token);

        await provider.SendAsync(message, context);

        Assert.Equal(cancellation.Token, store.LastCancellationToken);
        Assert.NotEqual(CancellationToken.None, store.LastCancellationToken);
    }

    /// <summary>
    /// 守卫分支返回的结果可被上下文按提供者归集
    /// </summary>
    [Fact]
    public async Task SendAsync_GuardResult_CarriesProviderNameForContextAggregation()
    {
        var store = new FakeDingTalkConfigStore(null);
        var provider = new DingTalkBotProvider(store);
        var message = new BotMessage { Content = "构建失败" };
        var context = new BotContext(message, [], TestContext.Current.CancellationToken);

        var result = await provider.SendAsync(message, context);
        context.AddResult(provider.Name, result);

        Assert.True(context.HasFailures);
        Assert.False(context.IsSuccess);
        Assert.Equal(BotProviderNames.DingTalk, Assert.Single(context.Results).Provider);
    }

    /// <summary>
    /// 消息类型路由与真实推送需要真实机器人凭据
    /// </summary>
    [Fact]
    public void SendAsync_WithValidCredentials_RequiresRealRobot()
    {
        Assert.Skip("需要真实钉钉自定义机器人 access_token 与外网访问 oapi.dingtalk.com，CI 不具备");
    }
}
