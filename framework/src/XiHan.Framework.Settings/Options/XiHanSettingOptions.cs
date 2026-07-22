// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Collections;
using XiHan.Framework.Settings.Definitions;
using XiHan.Framework.Settings.Providers;

namespace XiHan.Framework.Settings.Options;

/// <summary>
/// 曦寒设置选项
/// </summary>
public class XiHanSettingOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Settings";

    /// <summary>
    /// 构造函数
    /// </summary>
    public XiHanSettingOptions()
    {
        DefinitionProviders = new TypeList<ISettingDefinitionProvider>();
        ValueProviders = new TypeList<ISettingValueProvider>();
        DeletedSettings = [];
        ReturnOriginalValueIfDecryptFailed = true;
    }

    /// <summary>
    /// 设置定义提供者
    /// </summary>
    public ITypeList<ISettingDefinitionProvider> DefinitionProviders { get; }

    /// <summary>
    /// 设置值提供者
    /// </summary>
    public ITypeList<ISettingValueProvider> ValueProviders { get; }

    /// <summary>
    /// 已删除的设置
    /// </summary>
    public HashSet<string> DeletedSettings { get; }

    /// <summary>
    /// 解密失败时是否返回原始值
    /// </summary>
    public bool ReturnOriginalValueIfDecryptFailed { get; set; }
}
