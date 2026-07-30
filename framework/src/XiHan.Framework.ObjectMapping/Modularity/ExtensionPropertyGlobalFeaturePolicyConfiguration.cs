// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.ObjectMapping.Modularity;

/// <summary>
/// 扩展属性全局功能策略配置
/// </summary>
public class ExtensionPropertyGlobalFeaturePolicyConfiguration
{
    /// <summary>
    /// 全局功能名称数组
    /// </summary>
    public string[] Features { get; set; } = [];

    /// <summary>
    /// 是否需要满足所有全局功能条件
    /// </summary>
    public bool RequiresAll { get; set; } = default!;
}
