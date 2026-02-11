#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ExtensionPropertyFeaturePolicyConfiguration
// Guid:a52a1166-9e68-4000-9b17-896b764dc435
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/05 07:27:33
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.ObjectMapping.Modularity;

/// <summary>
/// 扩展属性功能策略配置
/// </summary>
public class ExtensionPropertyFeaturePolicyConfiguration
{
    /// <summary>
    /// 功能名称数组
    /// </summary>
    public string[] Features { get; set; } = [];

    /// <summary>
    /// 是否需要满足所有功能条件
    /// </summary>
    public bool RequiresAll { get; set; } = default!;
}
