// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Email.Options;

namespace XiHan.Framework.Bot.Email.Tests.Options;

/// <summary>
/// <see cref="EmailMessageDataKeys"/> 键名常量测试
/// </summary>
/// <remarks>
/// 这些键名是调用方往 BotMessage.Data 里写扩展数据时的协议字面量，
/// 一旦改动会让所有既有调用方静默失效（读不到就回退默认），所以必须锁死字符串值。
/// </remarks>
public class EmailMessageDataKeysTests
{
    /// <summary>
    /// 收件人/抄送/密送/HTML 开关四个键名保持不变
    /// </summary>
    [Fact]
    public void Keys_HaveStableLiteralValues()
    {
        Assert.Equal("Email.To", EmailMessageDataKeys.EmailTo);
        Assert.Equal("Email.Cc", EmailMessageDataKeys.EmailCc);
        Assert.Equal("Email.Bcc", EmailMessageDataKeys.EmailBcc);
        Assert.Equal("Email.IsBodyHtml", EmailMessageDataKeys.EmailIsBodyHtml);
    }

    /// <summary>
    /// 四个键名互不相同
    /// </summary>
    /// <remarks>
    /// BotMessage.Data 使用 OrdinalIgnoreCase 比较器，因此重名判断也必须忽略大小写。
    /// </remarks>
    [Fact]
    public void Keys_AreDistinctIgnoringCase()
    {
        string[] keys =
        [
            EmailMessageDataKeys.EmailTo,
            EmailMessageDataKeys.EmailCc,
            EmailMessageDataKeys.EmailBcc,
            EmailMessageDataKeys.EmailIsBodyHtml
        ];

        var distinct = keys.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        Assert.Equal(keys.Length, distinct.Count);
    }

    /// <summary>
    /// 所有键名统一使用 Email. 前缀
    /// </summary>
    [Theory]
    [InlineData("Email.To")]
    [InlineData("Email.Cc")]
    [InlineData("Email.Bcc")]
    [InlineData("Email.IsBodyHtml")]
    public void Keys_UseEmailPrefix(string key)
    {
        Assert.StartsWith("Email.", key, StringComparison.Ordinal);
    }
}
