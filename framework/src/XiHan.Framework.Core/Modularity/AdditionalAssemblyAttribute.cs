#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AdditionalAssemblyAttribute
// Guid:218559cc-c96b-4f29-a2ad-76d5d684f703
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/04/28 09:15:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
