// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using System.Runtime.Loader;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Utils.Collections;
using XiHan.Framework.Utils.Diagnostics;
using XiHan.Framework.Utils.Reflections;

namespace XiHan.Framework.Core.Modularity.PlugIns;

/// <summary>
/// 文件夹插件源
/// </summary>
public class FolderPlugInSource : IPlugInSource
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="folder"></param>
    /// <param name="searchOption"></param>
    public FolderPlugInSource(string folder, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        Guard.NotNull(folder, nameof(folder));

        Folder = folder;
        SearchOption = searchOption;
    }

    /// <summary>
    /// 文件夹
    /// </summary>
    public string Folder { get; }

    /// <summary>
    /// 搜索选项
    /// </summary>
    public SearchOption SearchOption { get; set; }

    /// <summary>
    /// 过滤器
    /// </summary>
    public Func<string, bool>? Filter { get; set; }

    /// <summary>
    /// 获取模块
    /// </summary>
    /// <returns></returns>
    /// <exception cref="XiHanException"></exception>
    public Type[] GetModules()
    {
        List<Type> modules = [];

        foreach (var assembly in GetAssemblies())
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (XiHanModuleHelper.IsXiHanModule(type))
                    {
                        modules.AddIfNotContains(type);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new XiHanException($"无法从程序集获取曦寒模块类型：{assembly.FullName}", ex);
            }
        }

        return [.. modules];
    }

    /// <summary>
    /// 获取程序集
    /// </summary>
    /// <returns></returns>
    private List<Assembly> GetAssemblies()
    {
        var assemblyFiles = ReflectionHelper.GetAssemblyFiles(Folder, SearchOption);

        if (Filter is not null)
        {
            assemblyFiles = assemblyFiles.Where(Filter);
        }

        return [.. assemblyFiles.Select(AssemblyLoadContext.Default.LoadFromAssemblyPath)];
    }
}
