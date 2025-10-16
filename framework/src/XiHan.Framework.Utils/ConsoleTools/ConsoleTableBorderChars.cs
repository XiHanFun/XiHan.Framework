#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TableBorderChars
// Guid:0174509c-7507-4507-a960-f1b377ef14c9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/17 5:23:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.ConsoleTools;

/// <summary>
/// 表格边框字符集
/// </summary>
internal static class ConsoleTableBorderChars
{
    /// <summary>
    /// 获取指定样式的边框字符集
    /// </summary>
    /// <param name="style">边框样式</param>
    /// <returns>边框字符集</returns>
    public static ConsoleTableBorderCharSet GetBorderChars(ConsoleTableBorderStyle style)
    {
        return style switch
        {
            ConsoleTableBorderStyle.None => new ConsoleTableBorderCharSet
            {
                TopLeft = ' ',
                TopRight = ' ',
                BottomLeft = ' ',
                BottomRight = ' ',
                TopCenter = ' ',
                BottomCenter = ' ',
                LeftCenter = ' ',
                RightCenter = ' ',
                Cross = ' ',
                Horizontal = ' ',
                Vertical = ' '
            },
            ConsoleTableBorderStyle.Simple => new ConsoleTableBorderCharSet
            {
                TopLeft = '+',
                TopRight = '+',
                BottomLeft = '+',
                BottomRight = '+',
                TopCenter = '+',
                BottomCenter = '+',
                LeftCenter = '+',
                RightCenter = '+',
                Cross = '+',
                Horizontal = '-',
                Vertical = '|'
            },
            ConsoleTableBorderStyle.Rounded => new ConsoleTableBorderCharSet
            {
                TopLeft = '╭',
                TopRight = '╮',
                BottomLeft = '╰',
                BottomRight = '╯',
                TopCenter = '┬',
                BottomCenter = '┴',
                LeftCenter = '├',
                RightCenter = '┤',
                Cross = '┼',
                Horizontal = '─',
                Vertical = '│'
            },
            ConsoleTableBorderStyle.Double => new ConsoleTableBorderCharSet
            {
                TopLeft = '╔',
                TopRight = '╗',
                BottomLeft = '╚',
                BottomRight = '╝',
                TopCenter = '╦',
                BottomCenter = '╩',
                LeftCenter = '╠',
                RightCenter = '╣',
                Cross = '╬',
                Horizontal = '═',
                Vertical = '║'
            },
            ConsoleTableBorderStyle.Bold => new ConsoleTableBorderCharSet
            {
                TopLeft = '┏',
                TopRight = '┓',
                BottomLeft = '┗',
                BottomRight = '┛',
                TopCenter = '┳',
                BottomCenter = '┻',
                LeftCenter = '┣',
                RightCenter = '┫',
                Cross = '╋',
                Horizontal = '━',
                Vertical = '┃'
            },
            ConsoleTableBorderStyle.Markdown => new ConsoleTableBorderCharSet
            {
                TopLeft = '|',
                TopRight = '|',
                BottomLeft = '|',
                BottomRight = '|',
                TopCenter = '|',
                BottomCenter = '|',
                LeftCenter = '|',
                RightCenter = '|',
                Cross = '|',
                Horizontal = '-',
                Vertical = '|'
            },
            _ => GetBorderChars(ConsoleTableBorderStyle.Simple)
        };
    }
}

/// <summary>
/// 表格边框样式
/// </summary>
public enum ConsoleTableBorderStyle
{
    /// <summary>
    /// 无边框
    /// </summary>
    None,

    /// <summary>
    /// 简单边框（使用 ASCII 字符）
    /// </summary>
    Simple,

    /// <summary>
    /// 圆角边框
    /// </summary>
    Rounded,

    /// <summary>
    /// 双线边框
    /// </summary>
    Double,

    /// <summary>
    /// 粗体边框
    /// </summary>
    Bold,

    /// <summary>
    /// Markdown 样式
    /// </summary>
    Markdown
}

/// <summary>
/// 边框字符集
/// </summary>
internal class ConsoleTableBorderCharSet
{
    /// <summary>
    /// 左上角
    /// </summary>
    public char TopLeft { get; set; }

    /// <summary>
    /// 右上角
    /// </summary>
    public char TopRight { get; set; }

    /// <summary>
    /// 左下角
    /// </summary>
    public char BottomLeft { get; set; }

    /// <summary>
    /// 右下角
    /// </summary>
    public char BottomRight { get; set; }

    /// <summary>
    /// 上中
    /// </summary>
    public char TopCenter { get; set; }

    /// <summary>
    /// 下中
    /// </summary>
    public char BottomCenter { get; set; }

    /// <summary>
    /// 左中
    /// </summary>
    public char LeftCenter { get; set; }

    /// <summary>
    /// 右中
    /// </summary>
    public char RightCenter { get; set; }

    /// <summary>
    /// 交叉
    /// </summary>
    public char Cross { get; set; }

    /// <summary>
    /// 水平
    /// </summary>
    public char Horizontal { get; set; }

    /// <summary>
    /// 垂直
    /// </summary>
    public char Vertical { get; set; }
}
