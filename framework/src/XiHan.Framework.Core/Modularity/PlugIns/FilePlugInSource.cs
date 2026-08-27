// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
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
            // 原实现把程序集加载放在 try 之外：相对路径、文件缺失、非托管映像等加载失败会以
            // ArgumentException / FileNotFoundException / BadImageFormatException 原样抛出，
            // 与扫描阶段统一包装成 XiHanException 的错误契约不一致，上层也无从知道是哪个插件文件出的问题。
            // 加载与扫描同属一次插件解析，这里把加载并入同一套包装，并在消息里带上出问题的文件路径。
            Assembly assembly;
            try
            {
                assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(filePath);
            }
            catch (Exception ex)
            {
                throw new XiHanException($"无法加载插件程序集文件：{filePath}", ex);
            }

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
