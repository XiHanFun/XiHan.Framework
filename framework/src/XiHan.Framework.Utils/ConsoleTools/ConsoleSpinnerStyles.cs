#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ConsoleSpinnerStyles
// Guid:660ad0d2-80c3-4b87-abd9-d7ea7c0e4301
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/17 05:21:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.ConsoleTools;

/// <summary>
/// 预定义的旋转器样式
/// </summary>
public static class ConsoleSpinnerStyles
{
    /// <summary>传统旋转器 |/-\</summary>
    public static readonly string[] Classic = ["|", "/", "-", "\\"];

    /// <summary>点旋转器</summary>
    public static readonly string[] Dots = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

    /// <summary>箭头旋转器</summary>
    public static readonly string[] Arrow = ["←", "↖", "↑", "↗", "→", "↘", "↓", "↙"];

    /// <summary>方块旋转器</summary>
    public static readonly string[] Block = ["▖", "▘", "▝", "▗"];

    /// <summary>时钟旋转器</summary>
    public static readonly string[] Clock = ["🕐", "🕑", "🕒", "🕓", "🕔", "🕕", "🕖", "🕗", "🕘", "🕙", "🕚", "🕛"];

    /// <summary>月亮旋转器</summary>
    public static readonly string[] Moon = ["🌑", "🌒", "🌓", "🌔", "🌕", "🌖", "🌗", "🌘"];
}
