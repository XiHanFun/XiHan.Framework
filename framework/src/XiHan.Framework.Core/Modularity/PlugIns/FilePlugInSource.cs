// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Runtime.Loader;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.Core.Modularity.PlugIns;

/// <summary>
/// 文件插件源
/// </summary>
public class FilePlugInSource : IPlugInSource
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="filePaths"></param>
    public FilePlugInSource(params string[]? filePaths)
    {
        FilePaths = filePaths ?? [];
    }

    /// <summary>
    /// 文件路径
    /// </summary>
    public string[] FilePaths { get; }

    /// <summary>
    /// 获取模块
    /// </summary>
    /// <returns></returns>
    /// <exception cref="XiHanException"></exception>
    public Type[] GetModules()
    {
        List<Type> modules = [];

        foreach (var filePath in FilePaths)
        {
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(filePath);

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
}
