// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Sms.Options;

namespace XiHan.Framework.Bot.Sms.Tests.Options;

/// <summary>
/// <see cref="SmsMessageDataKeys"/> 短信消息扩展数据键名测试
/// </summary>
/// <remarks>
/// 这些键名是调用方（业务侧组装 BotMessage.Data）与 SmsBotProvider 之间的隐式协议，
/// 改字面量会让存量调用方静默失去手机号/模板码，因此逐个锁死。
/// </remarks>
public class SmsMessageDataKeysTests
{
    /// <summary>
    /// 手机号键名固定为 Sms.PhoneNumbers
    /// </summary>
    [Fact]
    public void PhoneNumbers_KeyIsPinned()
    {
        Assert.Equal("Sms.PhoneNumbers", SmsMessageDataKeys.PhoneNumbers);
    }

    /// <summary>
    /// 模板码键名固定为 Sms.TemplateCode
    /// </summary>
    [Fact]
    public void TemplateCode_KeyIsPinned()
    {
        Assert.Equal("Sms.TemplateCode", SmsMessageDataKeys.TemplateCode);
    }

    /// <summary>
    /// 模板参数键名固定为 Sms.TemplateParams
    /// </summary>
    [Fact]
    public void TemplateParams_KeyIsPinned()
    {
        Assert.Equal("Sms.TemplateParams", SmsMessageDataKeys.TemplateParams);
    }

    /// <summary>
    /// 三个键名互不相同，避免 Data 字典键位相互覆盖
    /// </summary>
    [Fact]
    public void Keys_AreDistinct()
    {
        var keys = new[]
        {
            SmsMessageDataKeys.PhoneNumbers,
            SmsMessageDataKeys.TemplateCode,
            SmsMessageDataKeys.TemplateParams
        };

        Assert.Equal(keys.Length, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
