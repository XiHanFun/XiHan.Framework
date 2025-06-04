#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IHasNameWithLocalizableDisplayName
// Guid:cc566928-8442-4b6b-a3c7-ef5884a46830
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/5 6:57:12
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics.CodeAnalysis;

namespace XiHan.Framework.Localization;

/// <summary>
/// 具有名称和可本地化显示名称的接口
/// 用于定义具有标识名称和支持多语言显示名称的对象
/// </summary>
public interface IHasNameWithLocalizableDisplayName
{
    /// <summary>
    /// 获取对象的标识名称
    /// 通常用作内部标识符，不进行本地化处理
    /// </summary>
    [NotNull]
    public string Name { get; }

    /// <summary>
    /// 获取可本地化的显示名称
    /// 用于用户界面显示，支持多语言本地化
    /// </summary>
    public ILocalizableString? DisplayName { get; }
}
