#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanSettingOptions
// Guid:37d97385-094d-4fe9-a77c-f288f5fa728d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/25 4:04:24
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Collections;
using XiHan.Framework.Settings.Providers;
using XiHan.Framework.Settings.Stores;

namespace XiHan.Framework.Settings.Options;

/// <summary>
/// 曦寒设置选项
/// </summary>
public class XiHanSettingOptions
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public XiHanSettingOptions()
    {
        ValueProviders = new TypeList<ISettingValueProvider>();
    }

    /// <summary>
    /// 默认作用域
    /// </summary>
    public SettingScope DefaultScope { get; set; } = SettingScope.Application;

    /// <summary>
    /// 值提供者
    /// </summary>
    public ITypeList<ISettingValueProvider> ValueProviders { get; }

    /// <summary>
    /// 自动加载预定义设置
    /// </summary>
    public bool AutoLoadDefinitions { get; set; } = true;

    /// <summary>
    /// 加密选项
    /// </summary>
    public XiHanAesOptions Encryption { get; set; } = new();
}
