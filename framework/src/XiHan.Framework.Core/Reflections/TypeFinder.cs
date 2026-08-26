// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Core.Logging;
using XiHan.Framework.Utils.Reflections;

namespace XiHan.Framework.Core.Reflections;

/// <summary>
/// 类型查找器
/// </summary>
public class TypeFinder : ITypeFinder
{
    private readonly IAssemblyFinder _assemblyFinder;
    private readonly IInitLogger<TypeFinder> _logger;
    private readonly Lazy<IReadOnlyList<Type>> _types;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="assemblyFinder">程序集查找器</param>
    /// <param name="initLoggerFactory">初始化日志工厂，缺省使用进程内默认实现</param>
    public TypeFinder(IAssemblyFinder assemblyFinder, IInitLoggerFactory? initLoggerFactory = null)
    {
        _assemblyFinder = assemblyFinder;
        _logger = (initLoggerFactory ?? new DefaultInitLoggerFactory()).Create<TypeFinder>();
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
        List<Type> allTypes = [];

        foreach (var assembly in _assemblyFinder.Assemblies)
        {
            try
            {
                // ReflectionHelper.GetAllTypes 已消化 ReflectionTypeLoadException，
                // 这里捕获的是依赖缺失等其它程序集加载失败——跳过并留痕，避免静默吞掉。
                allTypes.AddRange(ReflectionHelper.GetAllTypes(assembly));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "加载程序集 {Assembly} 的类型失败，已跳过该程序集的类型扫描。", assembly.FullName);
            }
        }

        return allTypes;
    }
}
