// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.Modularity;

/// <summary>
/// 模块容器接口
/// </summary>
public interface IModuleContainer
{
    /// <summary>
    /// 模块列表
    /// </summary>
    IReadOnlyList<IModuleDescriptor> Modules { get; }
}
