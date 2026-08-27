// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using XiHan.Framework.Bot.WeCom.Options;

namespace XiHan.Framework.Bot.WeCom.Tests.Options;

/// <summary>
/// <see cref="WeComMessageDataKeys"/> 键名常量测试
/// </summary>
/// <remarks>
/// 这些常量是调用方往 <c>BotMessage.Data</c> 里塞企业微信专属负载时用的字符串键，
/// 一旦改动会让上层已写好的调用静默失效（提供者查不到数据会退化成纯文本），所以必须锁死字面量。
/// </remarks>
public class WeComMessageDataKeysTests
{
    /// <summary>
    /// 键名字面量与已发布契约一致
    /// </summary>
    /// <param name="actual">常量值</param>
    /// <param name="expected">期望字面量</param>
    [Theory]
    [InlineData(WeComMessageDataKeys.WeComNews, "WeCom.News")]
    [InlineData(WeComMessageDataKeys.WeComImage, "WeCom.Image")]
    [InlineData(WeComMessageDataKeys.WeComFile, "WeCom.File")]
    [InlineData(WeComMessageDataKeys.WeComVoice, "WeCom.Voice")]
    [InlineData(WeComMessageDataKeys.WeComTemplateCardTextNotice, "WeCom.TemplateCardTextNotice")]
    [InlineData(WeComMessageDataKeys.WeComTemplateCardNewsNotice, "WeCom.TemplateCardNewsNotice")]
    public void Keys_MatchPublishedContract(string actual, string expected)
    {
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// 所有键名互不重复
    /// </summary>
    /// <remarks>
    /// 消息 Data 字典用的是忽略大小写的比较器，重复键会让两类负载互相覆盖，这里按忽略大小写去重校验。
    /// </remarks>
    [Fact]
    public void Keys_AreUniqueIgnoringCase()
    {
        var keys = typeof(WeComMessageDataKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false })
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();

        Assert.Equal(6, keys.Length);
        Assert.Equal(keys.Length, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>
    /// 所有键名统一以 WeCom. 前缀命名
    /// </summary>
    [Fact]
    public void Keys_AllUseWeComPrefix()
    {
        Assert.StartsWith("WeCom.", WeComMessageDataKeys.WeComNews, StringComparison.Ordinal);
        Assert.StartsWith("WeCom.", WeComMessageDataKeys.WeComImage, StringComparison.Ordinal);
        Assert.StartsWith("WeCom.", WeComMessageDataKeys.WeComFile, StringComparison.Ordinal);
        Assert.StartsWith("WeCom.", WeComMessageDataKeys.WeComVoice, StringComparison.Ordinal);
        Assert.StartsWith("WeCom.", WeComMessageDataKeys.WeComTemplateCardTextNotice, StringComparison.Ordinal);
        Assert.StartsWith("WeCom.", WeComMessageDataKeys.WeComTemplateCardNewsNotice, StringComparison.Ordinal);
    }
}
