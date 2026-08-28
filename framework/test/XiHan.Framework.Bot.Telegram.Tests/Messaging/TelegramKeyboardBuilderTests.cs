// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Telegram.Bot.Types.ReplyMarkups;
using XiHan.Framework.Bot.Telegram.Messaging;

namespace XiHan.Framework.Bot.Telegram.Tests.Messaging;

/// <summary>
/// <see cref="TelegramKeyboardBuilder"/> 内联键盘构建器测试
/// </summary>
/// <remarks>
/// 键盘按钮的 callback data 必须与回调路由表的 <c>action:id</c> 约定严格一致，
/// 否则用户点了按钮什么都不会发生（回调路由查不到 action）。
/// 这里逐条锁死行的切分规则与 callback data 的拼接格式。
/// </remarks>
public class TelegramKeyboardBuilderTests
{
    /// <summary>
    /// 未添加任何按钮时构建出空键盘
    /// </summary>
    [Fact]
    public void Build_WhenNoButton_ProducesEmptyKeyboard()
    {
        var markup = new TelegramKeyboardBuilder().Build();

        Assert.Empty(ToRows(markup));
    }

    /// <summary>
    /// 同一行内连续添加的按钮保持添加顺序
    /// </summary>
    [Fact]
    public void AddButton_ConsecutiveButtonsStayInSameRow()
    {
        var markup = new TelegramKeyboardBuilder()
            .AddButton("确认", "confirm:1")
            .AddButton("取消", "cancel:1")
            .Build();

        var rows = ToRows(markup);

        Assert.Single(rows);
        Assert.Equal(2, rows[0].Count);
        Assert.Equal("确认", rows[0][0].Text);
        Assert.Equal("confirm:1", rows[0][0].CallbackData);
        Assert.Equal("取消", rows[0][1].Text);
        Assert.Equal("cancel:1", rows[0][1].CallbackData);
    }

    /// <summary>
    /// AddRow 结束当前行并开启新行
    /// </summary>
    [Fact]
    public void AddRow_StartsNewRow()
    {
        var markup = new TelegramKeyboardBuilder()
            .AddButton("第一行", "a:1")
            .AddRow()
            .AddButton("第二行", "b:1")
            .Build();

        var rows = ToRows(markup);

        Assert.Equal(2, rows.Count);
        Assert.Equal("第一行", rows[0][0].Text);
        Assert.Equal("第二行", rows[1][0].Text);
    }

    /// <summary>
    /// 当前行为空时 AddRow 不产生空行
    /// </summary>
    [Fact]
    public void AddRow_WhenCurrentRowEmpty_DoesNotEmitEmptyRow()
    {
        var markup = new TelegramKeyboardBuilder()
            .AddRow()
            .AddRow()
            .AddButton("唯一按钮", "a:1")
            .AddRow()
            .AddRow()
            .Build();

        var rows = ToRows(markup);

        Assert.Single(rows);
        Assert.Single(rows[0]);
    }

    /// <summary>
    /// Build 会自动收尾当前行，不需要调用方显式 AddRow
    /// </summary>
    [Fact]
    public void Build_FlushesPendingRow()
    {
        var markup = new TelegramKeyboardBuilder()
            .AddButton("第一行", "a:1")
            .AddRow()
            .AddButton("第二行", "b:1")
            .AddButton("第二行第二个", "b:2")
            .Build();

        var rows = ToRows(markup);

        Assert.Equal(2, rows.Count);
        Assert.Equal(2, rows[1].Count);
    }

    /// <summary>
    /// 按 action 与 id 拼接的按钮遵循 action:id 约定，action 首尾空白被裁剪
    /// </summary>
    [Fact]
    public void AddButton_WithActionAndId_JoinsWithSeparator()
    {
        var markup = new TelegramKeyboardBuilder()
            .AddButton("确认", "  confirm  ", "A-1")
            .Build();

        var rows = ToRows(markup);

        Assert.Equal("confirm:A-1", rows[0][0].CallbackData);
    }

    /// <summary>
    /// 链式方法返回构建器自身
    /// </summary>
    [Fact]
    public void FluentMethods_ReturnSameBuilder()
    {
        var builder = new TelegramKeyboardBuilder();

        Assert.Same(builder, builder.AddButton("确认", "confirm:1"));
        Assert.Same(builder, builder.AddUrlButton("官网", "https://example.com"));
        Assert.Same(builder, builder.AddRow());
    }

    /// <summary>
    /// 链接按钮只带 Url，不带回调数据
    /// </summary>
    [Fact]
    public void AddUrlButton_SetsUrlWithoutCallbackData()
    {
        var markup = new TelegramKeyboardBuilder()
            .AddUrlButton("官网", "https://example.com")
            .Build();

        var rows = ToRows(markup);

        Assert.Equal("官网", rows[0][0].Text);
        Assert.Equal("https://example.com", rows[0][0].Url);
        Assert.Null(rows[0][0].CallbackData);
    }

    /// <summary>
    /// 回调按钮只带回调数据，不带 Url
    /// </summary>
    [Fact]
    public void AddButton_SetsCallbackDataWithoutUrl()
    {
        var markup = new TelegramKeyboardBuilder()
            .AddButton("确认", "confirm:1")
            .Build();

        Assert.Null(ToRows(markup)[0][0].Url);
    }

    /// <summary>
    /// 按钮文本为空时抛参数异常
    /// </summary>
    /// <param name="text">按钮文本</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AddButton_WhenTextBlank_Throws(string? text)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new TelegramKeyboardBuilder().AddButton(text!, "confirm:1"));

        Assert.Equal("text", exception.ParamName);
    }

    /// <summary>
    /// 回调数据为空时抛参数异常
    /// </summary>
    /// <param name="callbackData">回调数据</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AddButton_WhenCallbackDataBlank_Throws(string? callbackData)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new TelegramKeyboardBuilder().AddButton("确认", callbackData!));

        Assert.Equal("callbackData", exception.ParamName);
    }

    /// <summary>
    /// 回调动作为空时抛参数异常
    /// </summary>
    /// <param name="action">回调动作名</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AddButton_WhenActionBlank_Throws(string? action)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new TelegramKeyboardBuilder().AddButton("确认", action!, "A-1"));

        Assert.Equal("action", exception.ParamName);
    }

    /// <summary>
    /// 链接按钮文本为空时抛参数异常
    /// </summary>
    [Fact]
    public void AddUrlButton_WhenTextBlank_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new TelegramKeyboardBuilder().AddUrlButton("   ", "https://example.com"));

        Assert.Equal("text", exception.ParamName);
    }

    /// <summary>
    /// 链接地址为空时抛参数异常
    /// </summary>
    [Fact]
    public void AddUrlButton_WhenUrlBlank_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new TelegramKeyboardBuilder().AddUrlButton("官网", "   "));

        Assert.Equal("url", exception.ParamName);
    }

    /// <summary>
    /// 确认 / 取消键盘生成同一行两个按钮，回调数据分别为 confirm:id 与 cancel:id
    /// </summary>
    [Fact]
    public void ConfirmCancel_ProducesTwoButtonsInOneRowWithDefaultActions()
    {
        var rows = ToRows(TelegramKeyboardBuilder.ConfirmCancel("A-1"));

        Assert.Single(rows);
        Assert.Equal(2, rows[0].Count);
        Assert.Equal("确认", rows[0][0].Text);
        Assert.Equal("confirm:A-1", rows[0][0].CallbackData);
        Assert.Equal("取消", rows[0][1].Text);
        Assert.Equal("cancel:A-1", rows[0][1].CallbackData);
    }

    /// <summary>
    /// 确认 / 取消键盘的动作名与文案均可自定义
    /// </summary>
    [Fact]
    public void ConfirmCancel_SupportsCustomActionsAndTexts()
    {
        var rows = ToRows(TelegramKeyboardBuilder.ConfirmCancel(
            "A-1",
            confirmAction: "order.approve",
            cancelAction: "order.reject",
            confirmText: "通过",
            cancelText: "驳回"));

        Assert.Equal("通过", rows[0][0].Text);
        Assert.Equal("order.approve:A-1", rows[0][0].CallbackData);
        Assert.Equal("驳回", rows[0][1].Text);
        Assert.Equal("order.reject:A-1", rows[0][1].CallbackData);
    }

    /// <summary>
    /// 确认 / 取消键盘的业务标识为空时抛参数异常
    /// </summary>
    /// <param name="id">业务标识</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ConfirmCancel_WhenIdBlank_Throws(string? id)
    {
        var exception = Assert.Throws<ArgumentException>(() => TelegramKeyboardBuilder.ConfirmCancel(id!));

        Assert.Equal("id", exception.ParamName);
    }

    /// <summary>
    /// 单按钮键盘生成一行一个按钮
    /// </summary>
    [Fact]
    public void Single_ProducesOneRowWithOneButton()
    {
        var rows = ToRows(TelegramKeyboardBuilder.Single("刷新", "refresh:A-1"));

        Assert.Single(rows);
        Assert.Single(rows[0]);
        Assert.Equal("刷新", rows[0][0].Text);
        Assert.Equal("refresh:A-1", rows[0][0].CallbackData);
    }

    /// <summary>
    /// 单按钮键盘的参数校验与 AddButton 一致
    /// </summary>
    [Fact]
    public void Single_WhenCallbackDataBlank_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => TelegramKeyboardBuilder.Single("刷新", "   "));

        Assert.Equal("callbackData", exception.ParamName);
    }

    /// <summary>
    /// 每个构建器实例独立，构建后再复用不会串到已产出的键盘上
    /// </summary>
    [Fact]
    public void Build_TwoBuildersDoNotShareRows()
    {
        var first = new TelegramKeyboardBuilder().AddButton("A", "a:1").Build();
        var second = new TelegramKeyboardBuilder().AddButton("B", "b:1").Build();

        Assert.Equal("A", ToRows(first)[0][0].Text);
        Assert.Equal("B", ToRows(second)[0][0].Text);
    }

    /// <summary>
    /// 把内联键盘摊平成可索引的行列表
    /// </summary>
    /// <param name="markup">内联键盘标记</param>
    /// <returns>行列表</returns>
    private static List<List<InlineKeyboardButton>> ToRows(InlineKeyboardMarkup markup)
    {
        return [.. markup.InlineKeyboard.Select(row => row.ToList())];
    }
}
