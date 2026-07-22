// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;

namespace XiHan.Framework.Core.Reflections;

/// <summary>
/// 程序集查找器接口
/// </summary>
public interface IAssemblyFinder
{
    /// <summary>
    /// 获取程序集
    /// </summary>
    IReadOnlyList<Assembly> Assemblies { get; }
}
