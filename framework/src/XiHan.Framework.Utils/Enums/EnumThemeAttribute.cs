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

namespace XiHan.Framework.Utils.Enums;

/// <summary>
/// 主题特性
/// </summary>
[AttributeUsage(AttributeTargets.Enum | AttributeTargets.Field)]
public class EnumThemeAttribute : Attribute
{
    // 缓存
    private static readonly ConcurrentDictionary<ThemeType, ThemeColor> ThemeColorsCatch = new()
    {
        [ThemeType.Default] = new ThemeColor { Theme = "default", Color = "#35495E" },
        [ThemeType.Tertiary] = new ThemeColor { Theme = "tertiary", Color = "#697882" },
        [ThemeType.Primary] = new ThemeColor { Theme = "primary", Color = "#3B86FF" },
        [ThemeType.Info] = new ThemeColor { Theme = "info", Color = "#FFFFFF00" },
        [ThemeType.Success] = new ThemeColor { Theme = "success", Color = "#19BE6B" },
        [ThemeType.Warning] = new ThemeColor { Theme = "warning", Color = "#FF9900" },
        [ThemeType.Error] = new ThemeColor { Theme = "error", Color = "#ED4014" }
    };

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="type"></param>
    public EnumThemeAttribute(ThemeType type)
    {
        ThemeColor = ThemeColorsCatch[type];
    }

    /// <summary>
    /// 主题颜色
    /// </summary>
    public ThemeColor ThemeColor { get; set; }
}
