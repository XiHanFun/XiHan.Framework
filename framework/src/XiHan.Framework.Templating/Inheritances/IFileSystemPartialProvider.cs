// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
