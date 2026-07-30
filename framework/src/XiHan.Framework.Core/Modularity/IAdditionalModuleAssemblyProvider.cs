// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;

namespace XiHan.Framework.Core.Modularity;

/// <summary>
/// 附加模块组装提供器接口
/// </summary>
public interface IAdditionalModuleAssemblyProvider
{
    /// <summary>
    /// 获取程序集
    /// </summary>
    /// <returns></returns>
    Assembly[] GetAssemblies();
}
