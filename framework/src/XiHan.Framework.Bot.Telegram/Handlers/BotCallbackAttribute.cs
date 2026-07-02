#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BotCallbackAttribute
// Guid:8de3f9a8-47a8-4b63-ae12-b99da402c27f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
