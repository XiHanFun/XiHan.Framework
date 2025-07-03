#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EnumThemeAttribute
// Guid:59609904-5a28-41f1-9708-05cb390e885e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/17 3:30:15
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using XiHan.Framework.Utils.Themes;

namespace XiHan.Framework.Utils.Enums;

/// <summary>
/// 枚举主题特性
/// </summary>
[AttributeUsage(AttributeTargets.Enum | AttributeTargets.Field)]
public class EnumThemeAttribute : Attribute
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
    public EnumThemeAttribute(ThemeType type)
    {
        ThemeColor = ThemeColorsCatch.GetValueOrDefault(type, ThemeColorsCatch[ThemeType.Default]);
    }

    /// <summary>
    /// 构造函数 - 使用自定义主题和颜色
    /// </summary>
    /// <param name="theme">主题名称</param>
    /// <param name="color">颜色值</param>
    public EnumThemeAttribute(string theme, string color)
    {
        ThemeColor = new ThemeColor { Theme = theme, Color = color };
    }

    /// <summary>
    /// 构造函数 - 使用自定义主题、颜色和图标
    /// </summary>
    /// <param name="theme">主题名称</param>
    /// <param name="color">颜色值</param>
    /// <param name="icon">图标</param>
    public EnumThemeAttribute(string theme, string color, string icon)
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

/// <summary>
/// 枚举状态主题特性
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class EnumStatusThemeAttribute : EnumThemeAttribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="status">状态</param>
    public EnumStatusThemeAttribute(StatusType status) : base(GetStatusTheme(status))
    {
        Status = status;
    }

    /// <summary>
    /// 状态
    /// </summary>
    public StatusType Status { get; }

    /// <summary>
    /// 获取状态主题
    /// </summary>
    /// <param name="status">状态</param>
    /// <returns>主题类型</returns>
    private static ThemeType GetStatusTheme(StatusType status)
    {
        return status switch
        {
            StatusType.Active => ThemeType.Success,
            StatusType.Inactive => ThemeType.Secondary,
            StatusType.Pending => ThemeType.Warning,
            StatusType.Disabled => ThemeType.Dark,
            StatusType.Error => ThemeType.Error,
            StatusType.Processing => ThemeType.Primary,
            _ => ThemeType.Default
        };
    }
}

/// <summary>
/// 枚举优先级主题特性
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class EnumPriorityThemeAttribute : EnumThemeAttribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="priority">优先级</param>
    public EnumPriorityThemeAttribute(PriorityType priority) : base(GetPriorityTheme(priority))
    {
        Priority = priority;
    }

    /// <summary>
    /// 优先级
    /// </summary>
    public PriorityType Priority { get; }

    /// <summary>
    /// 获取优先级主题
    /// </summary>
    /// <param name="priority">优先级</param>
    /// <returns>主题类型</returns>
    private static ThemeType GetPriorityTheme(PriorityType priority)
    {
        return priority switch
        {
            PriorityType.Low => ThemeType.Secondary,
            PriorityType.Normal => ThemeType.Primary,
            PriorityType.High => ThemeType.Warning,
            PriorityType.Urgent => ThemeType.Error,
            PriorityType.Critical => ThemeType.Error,
            _ => ThemeType.Default
        };
    }
}

/// <summary>
/// 状态类型
/// </summary>
public enum StatusType
{
    /// <summary>
    /// 激活
    /// </summary>
    Active,

    /// <summary>
    /// 非激活
    /// </summary>
    Inactive,

    /// <summary>
    /// 等待中
    /// </summary>
    Pending,

    /// <summary>
    /// 禁用
    /// </summary>
    Disabled,

    /// <summary>
    /// 错误
    /// </summary>
    Error,

    /// <summary>
    /// 处理中
    /// </summary>
    Processing
}

/// <summary>
/// 优先级类型
/// </summary>
public enum PriorityType
{
    /// <summary>
    /// 低
    /// </summary>
    Low,

    /// <summary>
    /// 正常
    /// </summary>
    Normal,

    /// <summary>
    /// 高
    /// </summary>
    High,

    /// <summary>
    /// 紧急
    /// </summary>
    Urgent,

    /// <summary>
    /// 关键
    /// </summary>
    Critical
}
