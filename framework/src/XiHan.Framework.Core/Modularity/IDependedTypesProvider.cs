// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.Modularity;

/// <summary>
/// 依赖类型提供器接口
/// </summary>
public interface IDependedTypesProvider
{
    /// <summary>
    /// 获取依赖类型
    /// </summary>
    /// <returns></returns>
    Type[] GetDependedTypes();
}
