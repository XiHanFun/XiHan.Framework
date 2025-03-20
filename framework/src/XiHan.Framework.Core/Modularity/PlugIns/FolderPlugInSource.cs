#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FolderPlugInSource
// Guid:e5045ebd-415b-4956-815f-6d1e74b5ef89
// Author:Administrator
// Email:me@zhaifanhua.com
// CreateTime:2024-04-28 上午 09:47:43
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using System.Runtime.Loader;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Utils.Collections;
using XiHan.Framework.Utils.Reflections;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Core.Modularity.PlugIns;

/// <summary>
/// FolderPlugInSource
/// </summary>
public class FolderPlugInSource : IPlugInSource
{
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
    /// 构造函数
    /// </summary>
    /// <param name="folder"></param>
    /// <param name="searchOption"></param>
    public FolderPlugInSource(string folder, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        _ = CheckHelper.NotNull(folder, nameof(folder));

        Folder = folder;
        SearchOption = searchOption;
    }

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
                        _ = modules.AddIfNotContains(type);
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
        var assemblyFiles = AssemblyHelper.GetAssemblyFiles(Folder, SearchOption);

        if (Filter is not null)
        {
            assemblyFiles = assemblyFiles.Where(Filter);
        }

        return assemblyFiles.Select(AssemblyLoadContext.Default.LoadFromAssemblyPath).ToList();
    }
}
