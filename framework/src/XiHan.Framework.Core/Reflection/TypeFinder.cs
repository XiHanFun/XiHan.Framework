#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TypeFinder
// Guid:095cacf2-45ef-4715-bef8-27f206c3b423
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/24 23:05:11
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Reflections;

namespace XiHan.Framework.Core.Reflection;

/// <summary>
/// 类型查找器
/// </summary>
public class TypeFinder : ITypeFinder
{
    private readonly IAssemblyFinder _assemblyFinder;
    private readonly Lazy<IReadOnlyList<Type>> _types;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="assemblyFinder"></param>
    public TypeFinder(IAssemblyFinder assemblyFinder)
    {
        _assemblyFinder = assemblyFinder;
        _types = new Lazy<IReadOnlyList<Type>>(FindAll, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// 类型
    /// </summary>
    public IReadOnlyList<Type> Types => _types.Value;

    /// <summary>
    /// 查找所有类型
    /// </summary>
    /// <returns></returns>
    private List<Type> FindAll()
    {
        var allTypes = new List<Type>();

        foreach (var assembly in _assemblyFinder.Assemblies)
        {
            try
            {
                var typesInThisAssembly = AssemblyHelper.GetAllTypes(assembly);

                if (!typesInThisAssembly.Any())
                {
                    continue;
                }

                allTypes.AddRange(typesInThisAssembly.Where(type => type != null));
            }
            catch
            {
                //TODO: Trigger a global event?
            }
        }

        return allTypes;
    }
}