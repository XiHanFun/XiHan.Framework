// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
