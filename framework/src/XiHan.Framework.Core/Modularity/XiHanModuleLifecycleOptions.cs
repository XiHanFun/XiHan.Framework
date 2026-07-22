// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Collections;

namespace XiHan.Framework.Core.Modularity;

/// <summary>
/// 曦寒模块生命周期选项
/// </summary>
public class XiHanModuleLifecycleOptions
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public XiHanModuleLifecycleOptions()
    {
        Contributors = new TypeList<IModuleLifecycleContributor>();
    }

    /// <summary>
    /// 贡献者
    /// </summary>
    public ITypeList<IModuleLifecycleContributor> Contributors { get; }
}
