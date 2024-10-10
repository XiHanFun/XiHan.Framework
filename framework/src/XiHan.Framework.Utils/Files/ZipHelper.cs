#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2023 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ZipHelper
// Guid:85265278-4d78-4775-a590-07b938651804
// Author:Administrator
// Email:me@zhaifanhua.com
// CreateTime:2023-07-21 上午 09:30:46
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.IO.Compression;

namespace XiHan.Framework.Utils.Files;

/// <summary>
/// 压缩解压帮助类
/// </summary>
public static class ZipHelper
{
    /// <summary>
    /// 提取
    /// </summary>
    /// <param name="archivePath">存档路径</param>
    /// <param name="extractPath">提取路径</param>
    /// <exception cref="FileNotFoundException"></exception>
    public static void Extract(string archivePath, string extractPath)
    {
        if (!File.Exists(archivePath)) throw new FileNotFoundException("未找到存档。", archivePath);

        if (!Directory.Exists(extractPath)) Directory.CreateDirectory(extractPath);

        ZipFile.ExtractToDirectory(archivePath, extractPath);
    }

    /// <summary>
    /// 压缩
    /// </summary>
    /// <param name="sourceDirectory">源目录</param>
    /// <param name="archivePath">存档路径</param>
    /// <exception cref="DirectoryNotFoundException"></exception>
    public static void Compress(string sourceDirectory, string archivePath)
    {
        if (!Directory.Exists(sourceDirectory)) throw new DirectoryNotFoundException("找不到源目录。");

        ZipFile.CreateFromDirectory(sourceDirectory, archivePath, CompressionLevel.Optimal, false);
    }
}