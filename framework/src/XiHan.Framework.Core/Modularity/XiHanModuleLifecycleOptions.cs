#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanModuleLifecycleOptions
// Guid:f9aabfaa-2442-4f35-8ee8-8285b56c0646
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/27 2:19:13
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Collections;

namespace XiHan.Framework.Core.Modularity;

/// <summary>
/// 曦寒模块生命周期选项
/// </summary>
public class XiHanModuleLifecycleOptions
{
    /// <summary>
    /// 贡献者
    /// </summary>
    public ITypeList<IModuleLifecycleContributor> Contributors { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    public XiHanModuleLifecycleOptions()
    {
        Contributors = new TypeList<IModuleLifecycleContributor>();
    }
}
