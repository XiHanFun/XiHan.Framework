#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BotMessageType
// Guid:27672c47-1ae5-4104-bb56-08949d1df67f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 17:43:40
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Bot.Enums;

/// <summary>
/// Bot 消息类型
/// </summary>
public enum BotMessageType
{
    /// <summary>
    /// 纯文本
    /// </summary>
    Text,

    /// <summary>
    /// Markdown
    /// </summary>
    Markdown,

    /// <summary>
    /// 卡片
    /// </summary>
    Card,

    /// <summary>
    /// 图片
    /// </summary>
    Image,

    /// <summary>
    /// 文件
    /// </summary>
    File,

    /// <summary>
    /// 链接
    /// </summary>
    Link
}
