#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IFileSystemPartialProvider
// Guid:3f529dc0-c046-45c4-9913-a8a588dcb379
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 04:14:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Inheritances;

/// <summary>
/// 文件系统片段提供者
/// </summary>
public interface IFileSystemPartialProvider : IPartialTemplateProvider
{
    /// <summary>
    /// 根目录
    /// </summary>
    string RootDirectory { get; }

    /// <summary>
    /// 文件扩展名
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// 是否启用监听
    /// </summary>
    bool EnableWatching { get; }

    /// <summary>
    /// 添加搜索路径
    /// </summary>
    /// <param name="path">搜索路径</param>
    void AddSearchPath(string path);

    /// <summary>
    /// 移除搜索路径
    /// </summary>
    /// <param name="path">搜索路径</param>
    void RemoveSearchPath(string path);
}
