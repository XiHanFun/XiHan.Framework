#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DependsOnAttribute
// Guid:ecc3fe11-595c-4c91-afe1-e026ba6665d1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 8:07:08
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.Modularity;

/// <summary>
/// 类型依赖特性
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class DependsOnAttribute : Attribute, IDependedTypesProvider
{
    /// <summary>
    /// 依赖类型集合
    /// </summary>
    public Type[] DependedTypes { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dependedTypes"></param>
    public DependsOnAttribute(params Type[]? dependedTypes)
    {
        DependedTypes = dependedTypes ?? Type.EmptyTypes;
    }

    /// <summary>
    /// 获取依赖类型
    /// </summary>
    /// <returns></returns>
    public virtual Type[] GetDependedTypes()
    {
        return DependedTypes;
    }
}
