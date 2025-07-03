#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ThemeColor
// Guid:e700693b-6ce1-4d39-b40e-ab76a73d7e6f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/7/3 23:24:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Themes;

/// <summary>
/// 主题颜色
/// </summary>
public record ThemeColor
{
    /// <summary>
    /// 主题名称
    /// </summary>
    public string Theme { get; set; } = "default";

    /// <summary>
    /// 颜色值
    /// </summary>
    public string Color { get; set; } = "#35495E";

    /// <summary>
    /// 图标
    /// </summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// 背景色
    /// </summary>
    public string BackgroundColor { get; set; } = string.Empty;

    /// <summary>
    /// 边框色
    /// </summary>
    public string BorderColor { get; set; } = string.Empty;

    /// <summary>
    /// 文本色
    /// </summary>
    public string TextColor { get; set; } = string.Empty;

    /// <summary>
    /// CSS 类名
    /// </summary>
    public string CssClass { get; set; } = string.Empty;

    /// <summary>
    /// 样式属性
    /// </summary>
    public Dictionary<string, string> Styles { get; set; } = [];

    /// <summary>
    /// 创建简单主题颜色
    /// </summary>
    /// <param name="theme">主题名称</param>
    /// <param name="color">颜色值</param>
    /// <returns>主题颜色</returns>
    public static ThemeColor Create(string theme, string color)
    {
        return new ThemeColor { Theme = theme, Color = color };
    }

    /// <summary>
    /// 创建完整主题颜色
    /// </summary>
    /// <param name="theme">主题名称</param>
    /// <param name="color">颜色值</param>
    /// <param name="icon">图标</param>
    /// <param name="backgroundColor">背景色</param>
    /// <param name="textColor">文本色</param>
    /// <returns>主题颜色</returns>
    public static ThemeColor Create(string theme, string color, string icon, string backgroundColor = "", string textColor = "")
    {
        return new ThemeColor
        {
            Theme = theme,
            Color = color,
            Icon = icon,
            BackgroundColor = backgroundColor,
            TextColor = textColor
        };
    }

    /// <summary>
    /// 添加样式
    /// </summary>
    /// <param name="property">CSS 属性</param>
    /// <param name="value">CSS 值</param>
    /// <returns>当前实例</returns>
    public ThemeColor WithStyle(string property, string value)
    {
        Styles[property] = value;
        return this;
    }

    /// <summary>
    /// 获取内联样式字符串
    /// </summary>
    /// <returns>内联样式字符串</returns>
    public string ToInlineStyle()
    {
        var styles = new List<string>();

        if (!string.IsNullOrEmpty(Color))
        {
            styles.Add($"color: {Color}");
        }

        if (!string.IsNullOrEmpty(BackgroundColor))
        {
            styles.Add($"background-color: {BackgroundColor}");
        }

        if (!string.IsNullOrEmpty(BorderColor))
        {
            styles.Add($"border-color: {BorderColor}");
        }

        foreach (var style in Styles)
        {
            styles.Add($"{style.Key}: {style.Value}");
        }

        return string.Join("; ", styles);
    }

    /// <summary>
    /// 获取完整的 CSS 类名
    /// </summary>
    /// <returns>CSS 类名</returns>
    public string GetCssClass()
    {
        var classes = new List<string>();

        if (!string.IsNullOrEmpty(Theme))
        {
            classes.Add($"theme-{Theme}");
        }

        if (!string.IsNullOrEmpty(CssClass))
        {
            classes.Add(CssClass);
        }

        return string.Join(" ", classes);
    }

    /// <summary>
    /// 转换为 CSS 变量
    /// </summary>
    /// <param name="prefix">前缀</param>
    /// <returns>CSS 变量字典</returns>
    public Dictionary<string, string> ToCssVariables(string prefix = "--theme")
    {
        var variables = new Dictionary<string, string>();

        if (!string.IsNullOrEmpty(Color))
        {
            variables[$"{prefix}-color"] = Color;
        }

        if (!string.IsNullOrEmpty(BackgroundColor))
        {
            variables[$"{prefix}-bg-color"] = BackgroundColor;
        }

        if (!string.IsNullOrEmpty(BorderColor))
        {
            variables[$"{prefix}-border-color"] = BorderColor;
        }

        if (!string.IsNullOrEmpty(TextColor))
        {
            variables[$"{prefix}-text-color"] = TextColor;
        }

        return variables;
    }
}
