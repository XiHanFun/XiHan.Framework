// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.Modularity;

/// <summary>
/// 类型依赖特性
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class DependsOnAttribute : Attribute, IDependedTypesProvider
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dependedTypes"></param>
    public DependsOnAttribute(params Type[]? dependedTypes)
    {
        DependedTypes = dependedTypes ?? Type.EmptyTypes;
    }

    /// <summary>
    /// 依赖类型集合
    /// </summary>
    public Type[] DependedTypes { get; }

    /// <summary>
    /// 获取依赖类型
    /// </summary>
    /// <returns></returns>
    public virtual Type[] GetDependedTypes()
    {
        return DependedTypes;
    }
}
