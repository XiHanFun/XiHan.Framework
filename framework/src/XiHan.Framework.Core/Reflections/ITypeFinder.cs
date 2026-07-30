// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.Reflections;

/// <summary>
/// 类型查找器接口
/// 它可能不会返回所有类型，但这些类型都与模块相关
/// </summary>
public interface ITypeFinder
{
    /// <summary>
    /// 类型列表
    /// </summary>
    IReadOnlyList<Type> Types { get; }
}
