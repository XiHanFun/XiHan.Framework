// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Bot.Telegram.Handlers;

/// <summary>
/// 标记按钮回调处理器绑定的 Action 前缀（用于 <see cref="IBotCallbackHandler"/>；
/// callback data 约定 action:id，取第一个冒号前为 action）
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class BotCallbackAttribute : Attribute
{
    /// <summary>
    /// 创建回调动作标记
    /// </summary>
    /// <param name="action">回调动作名（如 confirm:123 的 confirm）</param>
    public BotCallbackAttribute(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Action 不能为空。", nameof(action));
        }

        Action = action.Trim();
    }

    /// <summary>
    /// 回调动作名
    /// </summary>
    public string Action { get; }

    /// <summary>
    /// 是否仅管理员可执行
    /// </summary>
    public bool AdminOnly { get; set; }
}
