﻿#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FilePlugInSource
// Guid:4e8584f1-568a-4e8d-a345-774d80a55ef9
// Author:Administrator
// Email:me@zhaifanhua.com
// CreateTime:2024-04-28 上午 09:35:17
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using System.Runtime.Loader;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Utils.Extensions.System.Collections.Generic;

namespace XiHan.Framework.Core.Modularity.PlugIns;

/// <summary>
/// 文件插件源
/// </summary>
public class FilePlugInSource : IPlugInSource
{
    /// <summary>
    /// 文件路径
    /// </summary>
    public string[] FilePaths { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="filePaths"></param>
    public FilePlugInSource(params string[]? filePaths)
    {
        FilePaths = filePaths ?? [];
    }

    /// <summary>
    /// 获取模块
    /// </summary>
    /// <returns></returns>
    /// <exception cref="XiHanException"></exception>
    public Type[] GetModules()
    {
        List<Type>? modules = new();

        foreach (string? filePath in FilePaths)
        {
            Assembly? assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(filePath);

            try
            {
                foreach (Type? type in assembly.GetTypes())
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
