// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Email.Messaging;
using XiHan.Framework.Bot.Email.Options;
using XiHan.Framework.Bot.Email.Tests.Fakes;
using XiHan.Framework.Bot.Enums;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Providers;

namespace XiHan.Framework.Bot.Email.Tests.Messaging;

/// <summary>
/// <see cref="EmailBotProvider"/> 编排逻辑测试
/// </summary>
/// <remarks>
/// 全部用例都不连真实 SMTP。前置校验分支（未配置/禁用/发件人缺失/收件人为空）在投递之前就返回，
/// 天然无 IO；需要"证明收件人确实解析出来了"的分支，则把 SmtpPort 设成 -1：
/// MailKit 在建立连接之前就会因端口越界抛出参数异常，既不会解析外部域名也不会打开套接字，
/// 于是结果码从 BadRequest(400) 变成 Failed(500)——这个码差正是"是否走到了投递"的判据。
/// </remarks>
public class EmailBotProviderTests
{
    /// <summary>
    /// 提供者名称固定为 Email 常量
    /// </summary>
    [Fact]
    public void Name_IsEmailProviderName()
    {
        var provider = new EmailBotProvider(new FakeEmailConfigStore(null));

        Assert.Equal(BotProviderNames.Email, provider.Name);
        Assert.Equal("Email", provider.Name);
    }

    /// <summary>
    /// 提供者实现 IBotProvider 抽象
    /// </summary>
    [Fact]
    public void Type_ImplementsBotProviderAbstraction()
    {
        var provider = new EmailBotProvider(new FakeEmailConfigStore(null));

        Assert.IsAssignableFrom<IBotProvider>(provider);
    }

    /// <summary>
    /// 配置存储返回 null 时按未配置拒发
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenConfigIsNull_ReturnsBadRequest()
    {
        var store = new FakeEmailConfigStore(null);
        var provider = new EmailBotProvider(store);
        var message = CreateMessage();

        var result = await provider.SendAsync(message, CreateContext(message));

        AssertBadRequest(result, "not configured or disabled");
        Assert.Equal(1, store.GetCallCount);
    }

    /// <summary>
    /// 配置存在但被禁用时拒发
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenDisabled_ReturnsBadRequest()
    {
        var options = CreateSendableOptions();
        options.Enabled = false;
        options.To.Add("to@example.com");
        var provider = new EmailBotProvider(new FakeEmailConfigStore(options));
        var message = CreateMessage();

        var result = await provider.SendAsync(message, CreateContext(message));

        AssertBadRequest(result, "not configured or disabled");
    }

    /// <summary>
    /// 发件人主机或发件邮箱缺失时拒发
    /// </summary>
    /// <remarks>
    /// 空串与纯空白都视为缺失；该校验发生在收件人解析之前，所以即便收件人齐全也一样拒发。
    /// </remarks>
    [Theory]
    [InlineData("", "from@example.com")]
    [InlineData("   ", "from@example.com")]
    [InlineData("smtp.example.com", "")]
    [InlineData("smtp.example.com", "   ")]
    [InlineData("", "")]
    public async Task SendAsync_WhenSenderIncomplete_ReturnsBadRequest(string smtpHost, string fromMail)
    {
        var options = new EmailOptions
        {
            From =
            {
                SmtpHost = smtpHost,
                FromMail = fromMail
            }
        };
        options.To.Add("to@example.com");
        var provider = new EmailBotProvider(new FakeEmailConfigStore(options));
        var message = CreateMessage();

        var result = await provider.SendAsync(message, CreateContext(message));

        AssertBadRequest(result, "sender configuration");
    }

    /// <summary>
    /// 收件人、抄送、密送同时为空时拒发
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenAllRecipientListsEmpty_ReturnsBadRequest()
    {
        var provider = new EmailBotProvider(new FakeEmailConfigStore(CreateSendableOptions()));
        var message = CreateMessage();

        var result = await provider.SendAsync(message, CreateContext(message));

        AssertBadRequest(result, "recipients are required");
    }

    /// <summary>
    /// 仅配置抄送时通过收件人校验并进入投递
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenOnlyCcConfigured_ReachesDelivery()
    {
        var options = CreateSendableOptions();
        options.Cc.Add("cc@example.com");
        var provider = new EmailBotProvider(new FakeEmailConfigStore(options));
        var message = CreateMessage();

        var result = await provider.SendAsync(message, CreateContext(message));

        AssertReachedDelivery(result);
    }

    /// <summary>
    /// 仅配置密送时通过收件人校验并进入投递
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenOnlyBccConfigured_ReachesDelivery()
    {
        var options = CreateSendableOptions();
        options.Bcc.Add("bcc@example.com");
        var provider = new EmailBotProvider(new FakeEmailConfigStore(options));
        var message = CreateMessage();

        var result = await provider.SendAsync(message, CreateContext(message));

        AssertReachedDelivery(result);
    }

    /// <summary>
    /// 消息 Data 中的单个字符串收件人覆盖默认列表
    /// </summary>
    /// <remarks>
    /// 注意：字符串形态只产出一个收件人，不做逗号/分号拆分，多地址必须传字符串集合。
    /// </remarks>
    [Fact]
    public async Task SendAsync_WhenDataRecipientIsSingleString_ReachesDelivery()
    {
        var provider = new EmailBotProvider(new FakeEmailConfigStore(CreateSendableOptions()));
        var message = CreateMessage();
        message.Data[EmailMessageDataKeys.EmailTo] = "override@example.com";

        var result = await provider.SendAsync(message, CreateContext(message));

        AssertReachedDelivery(result);
    }

    /// <summary>
    /// 消息 Data 的键名大小写不敏感
    /// </summary>
    /// <remarks>
    /// BotMessage.Data 使用 OrdinalIgnoreCase 比较器，调用方写 email.to 也必须命中。
    /// </remarks>
    [Fact]
    public async Task SendAsync_WhenDataKeyCaseDiffers_StillResolvesRecipients()
    {
        var provider = new EmailBotProvider(new FakeEmailConfigStore(CreateSendableOptions()));
        var message = CreateMessage();
        message.Data["email.to"] = "override@example.com";

        var result = await provider.SendAsync(message, CreateContext(message));

        AssertReachedDelivery(result);
    }

    /// <summary>
    /// 字符串集合形态的收件人会过滤掉空白项
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenDataRecipientCollectionHasBlanks_KeepsOnlyValidOnes()
    {
        var provider = new EmailBotProvider(new FakeEmailConfigStore(CreateSendableOptions()));
        var message = CreateMessage();
        message.Data[EmailMessageDataKeys.EmailTo] = new[] { "  ", "override@example.com", string.Empty };

        var result = await provider.SendAsync(message, CreateContext(message));

        AssertReachedDelivery(result);
    }

    /// <summary>
    /// 字符串集合全为空白时收件人为空，拒发
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenDataRecipientCollectionIsAllBlank_ReturnsBadRequest()
    {
        var options = CreateSendableOptions();
        options.To.Add("fallback@example.com");
        var provider = new EmailBotProvider(new FakeEmailConfigStore(options));
        var message = CreateMessage();
        message.Data[EmailMessageDataKeys.EmailTo] = new[] { "  ", string.Empty };

        var result = await provider.SendAsync(message, CreateContext(message));

        // 显式给了集合就以集合为准，不再回落到默认收件人
        AssertBadRequest(result, "recipients are required");
    }

    /// <summary>
    /// 显式给出空集合时覆盖默认收件人并拒发
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenDataRecipientCollectionIsEmpty_ReturnsBadRequest()
    {
        var options = CreateSendableOptions();
        options.To.Add("fallback@example.com");
        var provider = new EmailBotProvider(new FakeEmailConfigStore(options));
        var message = CreateMessage();
        message.Data[EmailMessageDataKeys.EmailTo] = Array.Empty<string>();

        var result = await provider.SendAsync(message, CreateContext(message));

        AssertBadRequest(result, "recipients are required");
    }

    /// <summary>
    /// Data 中收件人为纯空白字符串时回落到默认收件人
    /// </summary>
    /// <remarks>
    /// 空白字符串既不满足 string 分支的非空白条件，也不是 IEnumerable&lt;string&gt;（string 只实现 IEnumerable&lt;char&gt;），
    /// 因此走兜底分支，用配置里的默认收件人。
    /// </remarks>
    [Fact]
    public async Task SendAsync_WhenDataRecipientIsWhitespace_FallsBackToOptions()
    {
        var options = CreateSendableOptions();
        options.To.Add("fallback@example.com");
        var provider = new EmailBotProvider(new FakeEmailConfigStore(options));
        var message = CreateMessage();
        message.Data[EmailMessageDataKeys.EmailTo] = "   ";

        var result = await provider.SendAsync(message, CreateContext(message));

        AssertReachedDelivery(result);
    }

    /// <summary>
    /// Data 中收件人为不支持的类型时回落到默认收件人
    /// </summary>
    [Theory]
    [InlineData(42)]
    [InlineData(true)]
    [InlineData(3.5)]
    public async Task SendAsync_WhenDataRecipientTypeUnsupported_FallsBackToOptions(object data)
    {
        var provider = new EmailBotProvider(new FakeEmailConfigStore(CreateSendableOptions()));
        var message = CreateMessage();
        message.Data[EmailMessageDataKeys.EmailTo] = data;

        var result = await provider.SendAsync(message, CreateContext(message));

        // 默认收件人也是空的，所以回落之后仍然是"收件人为空"
        AssertBadRequest(result, "recipients are required");
    }

    /// <summary>
    /// Data 中收件人是非字符串集合时回落到默认收件人
    /// </summary>
    /// <remarks>
    /// 模式匹配要求元素类型正好是 string，List&lt;int&gt; 这类集合不会被误当成收件人列表。
    /// </remarks>
    [Fact]
    public async Task SendAsync_WhenDataRecipientIsNonStringCollection_FallsBackToOptions()
    {
        var options = CreateSendableOptions();
        options.To.Add("fallback@example.com");
        var provider = new EmailBotProvider(new FakeEmailConfigStore(options));
        var message = CreateMessage();
        message.Data[EmailMessageDataKeys.EmailTo] = new List<int> { 1, 2 };

        var result = await provider.SendAsync(message, CreateContext(message));

        AssertReachedDelivery(result);
    }

    /// <summary>
    /// Data 中收件人显式为 null 时回落到默认收件人
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenDataRecipientIsNull_FallsBackToOptions()
    {
        var options = CreateSendableOptions();
        options.To.Add("fallback@example.com");
        var provider = new EmailBotProvider(new FakeEmailConfigStore(options));
        var message = CreateMessage();
        message.Data[EmailMessageDataKeys.EmailTo] = null;

        var result = await provider.SendAsync(message, CreateContext(message));

        AssertReachedDelivery(result);
    }

    /// <summary>
    /// 抄送与密送各自独立解析，互不串用
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenOnlyCcProvidedInData_ReachesDelivery()
    {
        var provider = new EmailBotProvider(new FakeEmailConfigStore(CreateSendableOptions()));
        var message = CreateMessage();
        message.Data[EmailMessageDataKeys.EmailCc] = "cc@example.com";

        var result = await provider.SendAsync(message, CreateContext(message));

        AssertReachedDelivery(result);
    }

    /// <summary>
    /// 密送单独出现在 Data 中时也能通过收件人校验
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenOnlyBccProvidedInData_ReachesDelivery()
    {
        var provider = new EmailBotProvider(new FakeEmailConfigStore(CreateSendableOptions()));
        var message = CreateMessage();
        message.Data[EmailMessageDataKeys.EmailBcc] = new List<string> { "bcc@example.com" };

        var result = await provider.SendAsync(message, CreateContext(message));

        AssertReachedDelivery(result);
    }

    /// <summary>
    /// 上下文取消令牌被原样透传给配置存储
    /// </summary>
    [Fact]
    public async Task SendAsync_PassesContextCancellationTokenToConfigStore()
    {
        using var cts = new CancellationTokenSource();
        var store = new FakeEmailConfigStore(null);
        var provider = new EmailBotProvider(store);
        var message = CreateMessage();
        var context = new BotContext(message, [BotProviderNames.Email], cts.Token);

        await provider.SendAsync(message, context);

        Assert.Equal(1, store.GetCallCount);
        Assert.Equal(cts.Token, store.LastCancellationToken);
    }

    /// <summary>
    /// 结果始终带上提供者名称，便于调度器归因
    /// </summary>
    [Fact]
    public async Task SendAsync_AlwaysStampsProviderName()
    {
        var provider = new EmailBotProvider(new FakeEmailConfigStore(null));
        var message = CreateMessage();

        var result = await provider.SendAsync(message, CreateContext(message));

        Assert.Equal(BotProviderNames.Email, result.Provider);
    }

    /// <summary>
    /// 真实 SMTP 投递成功路径需要可用凭据，CI 不具备
    /// </summary>
    [Fact]
    public void SendAsync_SuccessPath_RequiresRealSmtp()
    {
        Assert.Skip("需要真实 SMTP 凭据与网络，CI 不具备");
    }

    /// <summary>
    /// 构造一条最简消息
    /// </summary>
    private static BotMessage CreateMessage()
    {
        return new BotMessage
        {
            Title = "标题",
            Content = "正文"
        };
    }

    /// <summary>
    /// 构造调度上下文
    /// </summary>
    private static BotContext CreateContext(BotMessage message)
    {
        return new BotContext(message, [BotProviderNames.Email], TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// 构造一份能通过全部前置校验、但投递必然失败且不产生任何网络 IO 的配置
    /// </summary>
    private static EmailOptions CreateSendableOptions()
    {
        return new EmailOptions
        {
            Enabled = true,
            From =
            {
                SmtpHost = "localhost",
                // 端口越界：MailKit 在建立连接之前即抛参数异常，不会解析域名也不会打开套接字
                SmtpPort = -1,
                UseSsl = false,
                FromMail = "sender@example.com",
                FromName = "曦寒"
            }
        };
    }

    /// <summary>
    /// 断言结果是带指定关键字的请求错误
    /// </summary>
    private static void AssertBadRequest(BotResult result, string messageKeyword)
    {
        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Equal(BotResultCodes.BadRequest, result.Code);
        Assert.Equal(BotProviderNames.Email, result.Provider);
        Assert.NotNull(result.Message);
        Assert.Contains(messageKeyword, result.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 断言已经越过全部前置校验、真正走到了投递（并因非法端口失败）
    /// </summary>
    private static void AssertReachedDelivery(BotResult result)
    {
        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        // 500 而不是 400：说明收件人解析出了非空结果，前置校验全部通过
        Assert.Equal(BotResultCodes.Failed, result.Code);
        Assert.Equal(BotProviderNames.Email, result.Provider);
        Assert.Equal("Email send failed.", result.Message);
    }
}
