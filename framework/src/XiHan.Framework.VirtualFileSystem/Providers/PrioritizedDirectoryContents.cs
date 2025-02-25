#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PrioritizedDirectoryContents
// Guid:5caced57-142a-4c16-8b35-d4a8c991ac3c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/23 6:22:55
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.FileProviders;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace XiHan.Framework.VirtualFileSystem.Providers;

/// <summary>
/// 优先级目录内容包装器
/// </summary>
public class PrioritizedDirectoryContents : IDirectoryContents
{
    private readonly IDirectoryContents _inner;
    private readonly List<PrioritizedFileProvider> _providers;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="inner"></param>
    /// <param name="providers"></param>
    public PrioritizedDirectoryContents(IDirectoryContents inner, List<PrioritizedFileProvider> providers)
    {
        _inner = inner;
        _providers = providers;
    }

    /// <summary>
    /// 是否存在
    /// </summary>
    public bool Exists => _inner.Exists;

    /// <summary>
    /// 获取枚举器
    /// </summary>
    /// <returns></returns>
    IEnumerator<IFileInfo> IEnumerable<IFileInfo>.GetEnumerator()
    {
        // 收集所有文件，包括它们的提供程序优先级
        var allFiles = new Dictionary<string, Tuple<IFileInfo, int>>();
        
        // 遍历所有文件并追踪优先级最高的版本
        foreach (var file in _inner)
        {
            try
            {
                // 找到提供这个文件的提供者，并获取其优先级
                // 使用FirstOrDefault防止没有匹配时抛出异常
                var provider = _providers
                    .Where(p => p.Provider.GetFileInfo(file.Name).Exists)
                    .OrderByDescending(p => p.Priority)
                    .FirstOrDefault();
                
                if (provider != null)
                {
                    var priority = provider.Priority;
                    
                    // 如果这个文件尚未添加到字典中，或者新找到的文件优先级更高
                    if (!allFiles.ContainsKey(file.Name) || allFiles[file.Name].Item2 < priority)
                    {
                        allFiles[file.Name] = new Tuple<IFileInfo, int>(file, priority);
                    }
                }
            }
            catch
            {
                // 如果处理一个文件时出错，忽略它并继续
                continue;
            }
        }
        
        // 返回所有优先级最高的文件
        return allFiles.Values.Select(t => t.Item1).GetEnumerator();
    }

    /// <summary>
    /// 获取枚举器
    /// </summary>
    /// <returns></returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable<IFileInfo>)this).GetEnumerator();
    }
}
