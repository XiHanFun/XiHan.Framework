#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ThemeAttribute
// Guid:c0568958-8810-4302-a07a-839ed705c85a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/7/4 2:49:25
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;

namespace XiHan.Framework.Utils.Themes;

/// <summary>
/// 主题特性
/// </summary>
[AttributeUsage(AttributeTargets.Enum | AttributeTargets.Field)]
public class ThemeAttribute : Attribute
{
    // 预定义主题缓存
    private static readonly ConcurrentDictionary<ThemeType, ThemeColor> ThemeColorsCatch = new()
    {
        [ThemeType.Default] = new ThemeColor { Theme = "default", Color = "#35495E" },
        [ThemeType.Primary] = new ThemeColor { Theme = "primary", Color = "#3B86FF" },
        [ThemeType.Secondary] = new ThemeColor { Theme = "secondary", Color = "#6C757D" },
        [ThemeType.Tertiary] = new ThemeColor { Theme = "tertiary", Color = "#697882" },
        [ThemeType.Info] = new ThemeColor { Theme = "info", Color = "#909399" },
        [ThemeType.Success] = new ThemeColor { Theme = "success", Color = "#19BE6B" },
        [ThemeType.Warning] = new ThemeColor { Theme = "warning", Color = "#FF9900" },
        [ThemeType.Error] = new ThemeColor { Theme = "error", Color = "#ED4014" },
        [ThemeType.Light] = new ThemeColor { Theme = "light", Color = "#F8F9FA", TextColor = "#212529" },
        [ThemeType.Dark] = new ThemeColor { Theme = "dark", Color = "#343A40", TextColor = "#FFFFFF" },
        [ThemeType.Muted] = new ThemeColor { Theme = "muted", Color = "#6C757D" },
        [ThemeType.Link] = new ThemeColor { Theme = "link", Color = "#0D6EFD" },
        [ThemeType.Transparent] = new ThemeColor { Theme = "transparent", Color = "transparent" },
        [ThemeType.Custom] = new ThemeColor { Theme = "custom", Color = "#6C757D" }
    };

    /// <summary>
    /// 构造函数 - 使用预定义主题类型
    /// </summary>
    /// <param name="type">主题类型</param>
    public ThemeAttribute(ThemeType type)
    {
        ThemeColor = ThemeColorsCatch.GetValueOrDefault(type, ThemeColorsCatch[ThemeType.Default]);
    }

    /// <summary>
    /// 构造函数 - 使用自定义主题和颜色
    /// </summary>
    /// <param name="theme">主题名称</param>
    /// <param name="color">颜色值</param>
    public ThemeAttribute(string theme, string color)
    {
        ThemeColor = new ThemeColor { Theme = theme, Color = color };
    }

    /// <summary>
    /// 构造函数 - 使用自定义主题、颜色和图标
    /// </summary>
    /// <param name="theme">主题名称</param>
    /// <param name="color">颜色值</param>
    /// <param name="icon">图标</param>
    public ThemeAttribute(string theme, string color, string icon)
    {
        ThemeColor = new ThemeColor { Theme = theme, Color = color, Icon = icon };
    }

    /// <summary>
    /// 主题颜色
    /// </summary>
    public ThemeColor ThemeColor { get; set; }

    /// <summary>
    /// 获取预定义主题颜色
    /// </summary>
    /// <param name="type">主题类型</param>
    /// <returns>主题颜色</returns>
    public static ThemeColor GetPredefinedTheme(ThemeType type)
    {
        return ThemeColorsCatch.GetValueOrDefault(type, ThemeColorsCatch[ThemeType.Default]);
    }

    /// <summary>
    /// 注册自定义主题
    /// </summary>
    /// <param name="type">主题类型</param>
    /// <param name="themeColor">主题颜色</param>
    public static void RegisterTheme(ThemeType type, ThemeColor themeColor)
    {
        ThemeColorsCatch.AddOrUpdate(type, themeColor, (key, oldValue) => themeColor);
    }

    /// <summary>
    /// 获取所有预定义主题
    /// </summary>
    /// <returns>主题字典</returns>
    public static Dictionary<ThemeType, ThemeColor> GetAllPredefinedThemes()
    {
        return new Dictionary<ThemeType, ThemeColor>(ThemeColorsCatch);
    }
}
