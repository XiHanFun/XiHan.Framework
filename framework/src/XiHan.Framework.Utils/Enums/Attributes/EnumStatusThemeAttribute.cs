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

using XiHan.Framework.Utils.Enums.Enums;
using XiHan.Framework.Utils.Themes;

namespace XiHan.Framework.Utils.Enums.Attributes;

/// <summary>
/// 枚举状态主题特性
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class EnumStatusThemeAttribute : ThemeAttribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="status">状态</param>
    public EnumStatusThemeAttribute(EnumStatusType status) : base(GetStatusTheme(status))
    {
        Status = status;
    }

    /// <summary>
    /// 状态
    /// </summary>
    public EnumStatusType Status { get; }

    /// <summary>
    /// 获取状态主题
    /// </summary>
    /// <param name="status">状态</param>
    /// <returns>主题类型</returns>
    private static ThemeType GetStatusTheme(EnumStatusType status)
    {
        return status switch
        {
            EnumStatusType.Active => ThemeType.Success,
            EnumStatusType.Inactive => ThemeType.Secondary,
            EnumStatusType.Pending => ThemeType.Warning,
            EnumStatusType.Disabled => ThemeType.Dark,
            EnumStatusType.Error => ThemeType.Error,
            EnumStatusType.Processing => ThemeType.Primary,
            _ => ThemeType.Default
        };
    }
}
