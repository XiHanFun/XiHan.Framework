#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EnumPriorityThemeAttribute
// Guid:72949f0e-4b0c-462b-b069-f123f78b6b87
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/7/4 2:58:03
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Enums.Enums;
using XiHan.Framework.Utils.Themes;

namespace XiHan.Framework.Utils.Enums.Attributes;

/// <summary>
/// 枚举优先级主题特性
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class EnumPriorityThemeAttribute : ThemeAttribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="priority">优先级</param>
    public EnumPriorityThemeAttribute(EnumPriorityType priority) : base(GetPriorityTheme(priority))
    {
        Priority = priority;
    }

    /// <summary>
    /// 优先级
    /// </summary>
    public EnumPriorityType Priority { get; }

    /// <summary>
    /// 获取优先级主题
    /// </summary>
    /// <param name="priority">优先级</param>
    /// <returns>主题类型</returns>
    private static ThemeType GetPriorityTheme(EnumPriorityType priority)
    {
        return priority switch
        {
            EnumPriorityType.Low => ThemeType.Secondary,
            EnumPriorityType.Normal => ThemeType.Primary,
            EnumPriorityType.High => ThemeType.Warning,
            EnumPriorityType.Urgent => ThemeType.Error,
            EnumPriorityType.Critical => ThemeType.Error,
            _ => ThemeType.Default
        };
    }
}
