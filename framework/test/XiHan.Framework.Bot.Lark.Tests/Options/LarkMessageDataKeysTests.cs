// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Helpers;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Lark.Models;
using XiHan.Framework.Bot.Lark.Options;

namespace XiHan.Framework.Bot.Lark.Tests.Options;

/// <summary>
/// 飞书消息扩展数据键名常量测试
/// </summary>
/// <remarks>
/// 这三个常量是调用方往 <see cref="BotMessage.Data"/> 塞飞书专属载荷时的约定键，
/// 改名会让 LarkBotProvider 静默降级成纯文本，所以既锁字面量，也验证它们在
/// <see cref="BotMessage.Data"/> 的忽略大小写字典里能被稳定取出。
/// </remarks>
public class LarkMessageDataKeysTests
{
    /// <summary>
    /// 键名字面量保持稳定
    /// </summary>
    [Fact]
    public void Keys_Always_MatchDocumentedNames()
    {
        Assert.Equal("Lark.Post", LarkMessageDataKeys.LarkPost);
        Assert.Equal("Lark.InterActive", LarkMessageDataKeys.LarkInterActive);
        Assert.Equal("Lark.Image", LarkMessageDataKeys.LarkImage);
    }

    /// <summary>
    /// 键名互不重复（含忽略大小写比较）
    /// </summary>
    [Fact]
    public void Keys_Always_AreDistinctIgnoringCase()
    {
        var keys = new[]
        {
            LarkMessageDataKeys.LarkPost,
            LarkMessageDataKeys.LarkInterActive,
            LarkMessageDataKeys.LarkImage
        };

        Assert.Equal(keys.Length, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>
    /// 键名统一使用 Lark. 前缀，避免与其他提供者串键
    /// </summary>
    [Fact]
    public void Keys_Always_UseLarkPrefix()
    {
        Assert.StartsWith("Lark.", LarkMessageDataKeys.LarkPost);
        Assert.StartsWith("Lark.", LarkMessageDataKeys.LarkInterActive);
        Assert.StartsWith("Lark.", LarkMessageDataKeys.LarkImage);
    }

    /// <summary>
    /// 富文本载荷以任意大小写写入都能被常量取回
    /// </summary>
    [Theory]
    [InlineData("Lark.Post")]
    [InlineData("lark.post")]
    [InlineData("LARK.POST")]
    public void TryGetData_WhenPostStoredWithAnyCasing_IsResolvedByConstant(string storedKey)
    {
        var message = new BotMessage();
        message.Data[storedKey] = new LarkPost { Title = "release" };

        var found = BotMessageHelper.TryGetData<LarkPost>(message, LarkMessageDataKeys.LarkPost, out var post);

        Assert.True(found);
        Assert.NotNull(post);
        Assert.Equal("release", post.Title);
    }

    /// <summary>
    /// 消息卡片载荷可通过常量键取回
    /// </summary>
    [Fact]
    public void TryGetData_WhenInterActiveStored_IsResolvedByConstant()
    {
        var message = new BotMessage();
        var card = new LarkInterActive();
        card.Header.Title.Content = "card-title";
        message.Data[LarkMessageDataKeys.LarkInterActive] = card;

        var found = BotMessageHelper.TryGetData<LarkInterActive>(message, LarkMessageDataKeys.LarkInterActive, out var resolved);

        Assert.True(found);
        Assert.NotNull(resolved);
        Assert.Equal("card-title", resolved.Header.Title.Content);
    }

    /// <summary>
    /// 图片载荷可通过常量键取回
    /// </summary>
    [Fact]
    public void TryGetData_WhenImageStored_IsResolvedByConstant()
    {
        var message = new BotMessage();
        message.Data[LarkMessageDataKeys.LarkImage] = new LarkImage { ImageKey = "img_v2_abc" };

        var found = BotMessageHelper.TryGetData<LarkImage>(message, LarkMessageDataKeys.LarkImage, out var image);

        Assert.True(found);
        Assert.NotNull(image);
        Assert.Equal("img_v2_abc", image.ImageKey);
    }

    /// <summary>
    /// 键名存在但载荷类型不匹配时视为未提供
    /// </summary>
    [Fact]
    public void TryGetData_WhenTypeMismatch_ReturnsFalse()
    {
        var message = new BotMessage();
        message.Data[LarkMessageDataKeys.LarkImage] = "not-an-image";

        var found = BotMessageHelper.TryGetData<LarkImage>(message, LarkMessageDataKeys.LarkImage, out var image);

        Assert.False(found);
        Assert.Null(image);
    }

    /// <summary>
    /// 未写入载荷时取不到值
    /// </summary>
    [Fact]
    public void TryGetData_WhenKeyAbsent_ReturnsFalse()
    {
        var message = new BotMessage();

        var found = BotMessageHelper.TryGetData<LarkPost>(message, LarkMessageDataKeys.LarkPost, out var post);

        Assert.False(found);
        Assert.Null(post);
    }

    /// <summary>
    /// 三个键互不串扰
    /// </summary>
    [Fact]
    public void TryGetData_WhenOnlyImageStored_DoesNotResolveOtherKeys()
    {
        var message = new BotMessage();
        message.Data[LarkMessageDataKeys.LarkImage] = new LarkImage { ImageKey = "img_v2_abc" };

        Assert.False(BotMessageHelper.TryGetData<LarkPost>(message, LarkMessageDataKeys.LarkPost, out _));
        Assert.False(BotMessageHelper.TryGetData<LarkInterActive>(message, LarkMessageDataKeys.LarkInterActive, out _));
    }
}
