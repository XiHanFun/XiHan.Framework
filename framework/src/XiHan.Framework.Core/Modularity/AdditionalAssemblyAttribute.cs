// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;

namespace XiHan.Framework.Core.Modularity;

/// <summary>
/// 附加程序集特性
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class AdditionalAssemblyAttribute : Attribute, IAdditionalModuleAssemblyProvider
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="typesInAssemblies"></param>
    public AdditionalAssemblyAttribute(params Type[]? typesInAssemblies)
    {
        TypesInAssemblies = typesInAssemblies ?? Type.EmptyTypes;
    }

    /// <summary>
    /// 程序集类型
    /// </summary>
    public Type[] TypesInAssemblies { get; }

    /// <summary>
    /// 获取程序集
    /// </summary>
    /// <returns></returns>
    public virtual Assembly[] GetAssemblies()
    {
        return [.. TypesInAssemblies.Select(t => t.Assembly).Distinct()];
    }
}
