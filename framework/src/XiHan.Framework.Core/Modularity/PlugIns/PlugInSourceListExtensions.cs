#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PlugInSourceListExtensions
// Guid:fb4d8833-4b07-4904-b06a-32a4fe774411
// Author:Administrator
// Email:me@zhaifanhua.com
// CreateTime:2024-04-28 上午 09:52:29
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Core.Modularity.PlugIns;

/// <summary>
/// 插件源列表扩展
/// </summary>
public static class PlugInSourceListExtensions
{
    /// <summary>
    /// 添加文件夹
    /// </summary>
    /// <param name="list"></param>
    /// <param name="folder"></param>
    /// <param name="searchOption"></param>
    public static void AddFolder([NotNull] this PlugInSourceList list, [NotNull] string folder, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        _ = CheckHelper.NotNull(list, nameof(list));

        list.Add(new FolderPlugInSource(folder, searchOption));
    }

    /// <summary>
    /// 添加类型
    /// </summary>
    /// <param name="list"></param>
    /// <param name="moduleTypes"></param>
    public static void AddTypes([NotNull] this PlugInSourceList list, params Type[] moduleTypes)
    {
        _ = CheckHelper.NotNull(list, nameof(list));

        list.Add(new TypePlugInSource(moduleTypes));
    }

    /// <summary>
    /// 添加文件
    /// </summary>
    /// <param name="list"></param>
    /// <param name="filePaths"></param>
    public static void AddFiles([NotNull] this PlugInSourceList list, params string[] filePaths)
    {
        _ = CheckHelper.NotNull(list, nameof(list));

        list.Add(new FilePlugInSource(filePaths));
    }
}