// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;

namespace XiHan.Framework.VirtualFileSystem.Tests;

/// <summary>
/// 用例专属临时目录
/// </summary>
/// <remarks>
/// 物理提供器必须做真实文件读写才有验证价值，因此每个用例分配一个独立的 GUID 目录，
/// 释放时递归删除并吞掉异常——Windows 上文件句柄可能尚未完全释放，删除失败不应把用例判负。
/// </remarks>
internal sealed class TempDirectory : IDisposable
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public TempDirectory()
    {
        Root = Path.Combine(Path.GetTempPath(), "XiHanTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    /// <summary>
    /// 临时目录根路径
    /// </summary>
    public string Root { get; }

    /// <summary>
    /// 在临时目录下写入一个文本文件
    /// </summary>
    /// <param name="relativePath">相对路径，允许使用正斜杠</param>
    /// <param name="content">文本内容</param>
    /// <returns>文件的完整物理路径</returns>
    public string WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, content, new UTF8Encoding(false));
        return fullPath;
    }

    /// <summary>
    /// 在临时目录下创建子目录
    /// </summary>
    /// <param name="relativePath">相对路径</param>
    /// <returns>子目录的完整物理路径</returns>
    public string CreateSubDirectory(string relativePath)
    {
        var fullPath = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
