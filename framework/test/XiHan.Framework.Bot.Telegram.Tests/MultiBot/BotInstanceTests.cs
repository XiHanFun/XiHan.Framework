// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Telegram.MultiBot;
using XiHan.Framework.Bot.Telegram.Options;
using XiHan.Framework.Bot.Telegram.Tests.Fakes;

namespace XiHan.Framework.Bot.Telegram.Tests.MultiBot;

/// <summary>
/// <see cref="BotInstance"/> 机器人运行实例测试
/// </summary>
/// <remarks>
/// 构造实例只会创建 HttpClient 与 Bot 客户端对象，不产生任何网络请求，因此可以安全地在单测里建实例。
/// 这里的重点是两条权限判定：<see cref="BotInstance.IsAdmin"/> 与
/// <see cref="BotInstance.IsGroupAllowed"/>——后者是 fail-closed 语义（白名单为空 = 拒收所有群组），
/// 与「空 = 不限制」的直觉相反，一旦被改成直觉版本，机器人就会在任意群里可用。
/// </remarks>
public class BotInstanceTests
{
    /// <summary>
    /// 配置为空时抛参数空异常
    /// </summary>
    [Fact]
    public void Constructor_WhenConfigNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new BotInstance(null!));
    }

    /// <summary>
    /// 机器人名称为空时抛参数异常
    /// </summary>
    /// <param name="name">机器人名称</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenNameBlank_Throws(string name)
    {
        var config = TelegramTestFactory.CreateConfig(name: name);

        var exception = Assert.Throws<ArgumentException>(() => new BotInstance(config));

        Assert.Equal("config", exception.ParamName);
        Assert.Contains("Bot Name 不能为空", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Token 为空时抛参数异常，并在消息里带上机器人名称便于定位
    /// </summary>
    [Fact]
    public void Constructor_WhenTokenBlank_Throws()
    {
        var config = TelegramTestFactory.CreateConfig(name: "main-bot");
        config.Token = "   ";

        var exception = Assert.Throws<ArgumentException>(() => new BotInstance(config));

        Assert.Equal("config", exception.ParamName);
        Assert.Contains("main-bot", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 名称与 Token 的首尾空白被裁剪后保存
    /// </summary>
    [Fact]
    public void Constructor_TrimsNameAndToken()
    {
        var config = TelegramTestFactory.CreateConfig(name: "  main-bot  ");
        config.Token = $"  {TelegramTestFactory.ValidToken}  ";

        using var bot = new BotInstance(config);

        Assert.Equal("main-bot", bot.Name);
        Assert.Equal(TelegramTestFactory.ValidToken, bot.Token);
        Assert.Same(config, bot.Config);
        Assert.NotNull(bot.Client);
    }

    /// <summary>
    /// 未调用 SetIdentity 时身份信息为默认值
    /// </summary>
    [Fact]
    public void Identity_DefaultsAreZeroAndEmpty()
    {
        using var bot = TelegramTestFactory.CreateBot();

        Assert.Equal(0L, bot.BotId);
        Assert.Equal(string.Empty, bot.Username);
    }

    /// <summary>
    /// 回填身份信息时去掉用户名前的 @
    /// </summary>
    /// <param name="username">Telegram 返回的用户名</param>
    /// <param name="expected">保存结果</param>
    [Theory]
    [InlineData("my_bot", "my_bot")]
    [InlineData("@my_bot", "my_bot")]
    [InlineData("  @my_bot  ", "my_bot")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void SetIdentity_NormalizesUsername(string? username, string expected)
    {
        using var bot = TelegramTestFactory.CreateBot();

        bot.SetIdentity(12345L, username);

        Assert.Equal(12345L, bot.BotId);
        Assert.Equal(expected, bot.Username);
    }

    /// <summary>
    /// 管理员列表为空时任何人都不是管理员
    /// </summary>
    [Fact]
    public void IsAdmin_WhenNoAdminConfigured_ReturnsFalse()
    {
        using var bot = TelegramTestFactory.CreateBot();

        Assert.False(bot.IsAdmin(200L));
    }

    /// <summary>
    /// 管理员列表为 null 时不抛空引用
    /// </summary>
    [Fact]
    public void IsAdmin_WhenAdminUsersNull_ReturnsFalse()
    {
        var config = TelegramTestFactory.CreateConfig();
        config.AdminUsers = null!;

        using var bot = new BotInstance(config);

        Assert.False(bot.IsAdmin(200L));
    }

    /// <summary>
    /// 非正数用户 Id 一律不是管理员（0 表示取不到用户）
    /// </summary>
    /// <param name="userId">用户 Id</param>
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void IsAdmin_WhenUserIdNotPositive_ReturnsFalse(long userId)
    {
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(adminUsers: [0L, -1L, 200L]));

        Assert.False(bot.IsAdmin(userId));
    }

    /// <summary>
    /// 在管理员列表中的用户判定为管理员
    /// </summary>
    [Fact]
    public void IsAdmin_WhenUserInAdminList_ReturnsTrue()
    {
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(adminUsers: [200L, 300L]));

        Assert.True(bot.IsAdmin(200L));
        Assert.True(bot.IsAdmin(300L));
        Assert.False(bot.IsAdmin(400L));
    }

    /// <summary>
    /// 群组白名单为空时拒收所有群组（fail-closed）
    /// </summary>
    [Fact]
    public void IsGroupAllowed_WhenWhitelistEmpty_ReturnsFalse()
    {
        using var bot = TelegramTestFactory.CreateBot();

        Assert.False(bot.IsGroupAllowed(-100123L));
    }

    /// <summary>
    /// 群组白名单为 null 时同样拒收，不抛空引用
    /// </summary>
    [Fact]
    public void IsGroupAllowed_WhenWhitelistNull_ReturnsFalse()
    {
        var config = TelegramTestFactory.CreateConfig();
        config.AllowedGroupChatIds = null!;

        using var bot = new BotInstance(config);

        Assert.False(bot.IsGroupAllowed(-100123L));
    }

    /// <summary>
    /// 会话 Id 为 0 时一律拒绝（取不到会话就不放行）
    /// </summary>
    [Fact]
    public void IsGroupAllowed_WhenChatIdZero_ReturnsFalse()
    {
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(allowedGroupChatIds: [0L, -100123L]));

        Assert.False(bot.IsGroupAllowed(0L));
    }

    /// <summary>
    /// 白名单内的群组放行，白名单外的拒绝
    /// </summary>
    [Fact]
    public void IsGroupAllowed_ChecksWhitelistMembership()
    {
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(allowedGroupChatIds: [-100123L, -100456L]));

        Assert.True(bot.IsGroupAllowed(-100123L));
        Assert.True(bot.IsGroupAllowed(-100456L));
        Assert.False(bot.IsGroupAllowed(-100999L));
    }

    /// <summary>
    /// 配置了代理与自建 Bot API Server 时实例照常构建（构建阶段不发起任何连接）
    /// </summary>
    [Fact]
    public void Constructor_WithProxyAndCustomBaseUrl_BuildsClient()
    {
        var network = new TelegramBotNetworkOptions
        {
            ProxyUrl = "http://user:pass@127.0.0.1:7890",
            BaseUrl = "https://tg-api.example.com",
            TimeoutSeconds = 30
        };

        using var bot = new BotInstance(TelegramTestFactory.CreateConfig(), network);

        Assert.NotNull(bot.Client);
    }

    /// <summary>
    /// 超时秒数非正数时按默认值处理，不会构造出立即超时的客户端
    /// </summary>
    /// <param name="timeoutSeconds">超时秒数</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Constructor_WhenTimeoutNotPositive_StillBuildsClient(int timeoutSeconds)
    {
        var network = new TelegramBotNetworkOptions { TimeoutSeconds = timeoutSeconds };

        using var bot = new BotInstance(TelegramTestFactory.CreateConfig(), network);

        Assert.NotNull(bot.Client);
    }

    /// <summary>
    /// 不传网络配置时按默认网络配置构建
    /// </summary>
    [Fact]
    public void Constructor_WhenNetworkNull_UsesDefaults()
    {
        using var bot = new BotInstance(TelegramTestFactory.CreateConfig(), null);

        Assert.NotNull(bot.Client);
    }

    /// <summary>
    /// Token 格式非法（缺少 BotId 部分）时构造失败
    /// </summary>
    /// <remarks>
    /// Telegram.Bot 在构造客户端时就会从 Token 里解析 BotId，格式不对直接抛异常；
    /// 管理器依赖这一点把「配错 Token 的机器人」在启动阶段挡掉并记日志。
    /// </remarks>
    [Fact]
    public void Constructor_WhenTokenMalformed_Throws()
    {
        var config = TelegramTestFactory.CreateConfig();
        config.Token = "not-a-valid-token";

        Assert.Throws<ArgumentException>(() => new BotInstance(config));
    }

    /// <summary>
    /// 重复释放不抛异常（管理器的延迟释放与显式释放可能叠加）
    /// </summary>
    [Fact]
    public void Dispose_IsIdempotent()
    {
        var bot = TelegramTestFactory.CreateBot();

        bot.Dispose();
        bot.Dispose();
    }

    /// <summary>
    /// 实现 IDisposable，底层 HttpClient 由实例自己持有并释放
    /// </summary>
    [Fact]
    public void Type_IsDisposable()
    {
        using var bot = TelegramTestFactory.CreateBot();

        Assert.IsAssignableFrom<IDisposable>(bot);
    }
}
