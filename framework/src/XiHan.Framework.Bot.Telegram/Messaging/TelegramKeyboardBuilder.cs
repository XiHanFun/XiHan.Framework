// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Telegram.Bot.Types.ReplyMarkups;
using XiHan.Framework.Bot.Telegram.Options;

namespace XiHan.Framework.Bot.Telegram.Messaging;

/// <summary>
/// InlineKeyboard 流式构建器（callback data 约定 action:id）
/// </summary>
public sealed class TelegramKeyboardBuilder
{
    private readonly List<List<InlineKeyboardButton>> _rows = [];
    private List<InlineKeyboardButton> _currentRow = [];

    /// <summary>
    /// 追加一个回调按钮到当前行
    /// </summary>
    /// <param name="text">按钮文本</param>
    /// <param name="callbackData">完整回调数据</param>
    /// <returns>构建器自身</returns>
    public TelegramKeyboardBuilder AddButton(string text, string callbackData)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("text 不能为空。", nameof(text));
        }

        if (string.IsNullOrWhiteSpace(callbackData))
        {
            throw new ArgumentException("callbackData 不能为空。", nameof(callbackData));
        }

        _currentRow.Add(InlineKeyboardButton.WithCallbackData(text, callbackData));
        return this;
    }

    /// <summary>
    /// 追加一个回调按钮到当前行（按 action:id 约定拼接回调数据）
    /// </summary>
    /// <param name="text">按钮文本</param>
    /// <param name="action">回调动作名</param>
    /// <param name="id">业务标识</param>
    /// <returns>构建器自身</returns>
    public TelegramKeyboardBuilder AddButton(string text, string action, string id)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("action 不能为空。", nameof(action));
        }

        return AddButton(text, $"{action.Trim()}{TelegramBotPlatformConsts.CallbackDataSeparator}{id}");
    }

    /// <summary>
    /// 追加一个链接按钮到当前行
    /// </summary>
    /// <param name="text">按钮文本</param>
    /// <param name="url">链接地址</param>
    /// <returns>构建器自身</returns>
    public TelegramKeyboardBuilder AddUrlButton(string text, string url)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("text 不能为空。", nameof(text));
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("url 不能为空。", nameof(url));
        }

        _currentRow.Add(InlineKeyboardButton.WithUrl(text, url));
        return this;
    }

    /// <summary>
    /// 结束当前行并开启新行
    /// </summary>
    /// <returns>构建器自身</returns>
    public TelegramKeyboardBuilder AddRow()
    {
        if (_currentRow.Count > 0)
        {
            _rows.Add(_currentRow);
            _currentRow = [];
        }

        return this;
    }

    /// <summary>
    /// 构建内联键盘
    /// </summary>
    /// <returns>内联键盘标记</returns>
    public InlineKeyboardMarkup Build()
    {
        _ = AddRow();
        return new InlineKeyboardMarkup(_rows);
    }

    /// <summary>
    /// 生成「确认 / 取消」两按钮键盘（回调数据分别为 confirmAction:id 与 cancelAction:id）
    /// </summary>
    /// <param name="id">业务标识</param>
    /// <param name="confirmAction">确认动作名</param>
    /// <param name="cancelAction">取消动作名</param>
    /// <param name="confirmText">确认按钮文本</param>
    /// <param name="cancelText">取消按钮文本</param>
    /// <returns>内联键盘标记</returns>
    public static InlineKeyboardMarkup ConfirmCancel(
        string id,
        string confirmAction = "confirm",
        string cancelAction = "cancel",
        string confirmText = "确认",
        string cancelText = "取消")
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("id 不能为空。", nameof(id));
        }

        return new TelegramKeyboardBuilder()
            .AddButton(confirmText, confirmAction, id)
            .AddButton(cancelText, cancelAction, id)
            .Build();
    }

    /// <summary>
    /// 生成单按钮键盘
    /// </summary>
    /// <param name="text">按钮文本</param>
    /// <param name="callbackData">完整回调数据</param>
    /// <returns>内联键盘标记</returns>
    public static InlineKeyboardMarkup Single(string text, string callbackData)
    {
        return new TelegramKeyboardBuilder()
            .AddButton(text, callbackData)
            .Build();
    }
}
