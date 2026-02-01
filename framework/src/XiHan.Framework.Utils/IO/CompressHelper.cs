#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CompressHelper
// Guid:85265278-4d78-4775-a590-07b938651804
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2023/07/21 09:30:46
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.IO.Compression;

namespace XiHan.Framework.Utils.IO;

/// <summary>
/// 压缩帮助类
/// </summary>
public static class CompressHelper
{
    /// <summary>
    /// 解压文件
    /// </summary>
    /// <param name="archivePath">压缩文件路径</param>
    /// <param name="extractPath">解压目标路径</param>
    /// <param name="format">压缩格式</param>
    /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
    /// <exception cref="DirectoryNotFoundException">目录不存在时抛出</exception>
    public static void Extract(string archivePath, string extractPath, CompressionFormat format = CompressionFormat.Zip)
    {
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException("没有找到文件。", archivePath);
        }

        if (!Directory.Exists(extractPath))
        {
            Directory.CreateDirectory(extractPath);
        }

        switch (format)
        {
            case CompressionFormat.Zip:
                ZipFile.ExtractToDirectory(archivePath, extractPath);
                break;

            case CompressionFormat.GZip:
                ExtractGZip(archivePath, extractPath);
                break;

            case CompressionFormat.Deflate:
                ExtractDeflate(archivePath, extractPath);
                break;

            default:
                throw new ArgumentException("不支持的压缩格式。", nameof(format));
        }
    }

    /// <summary>
    /// 压缩文件或目录
    /// </summary>
    /// <param name="sourcePath">源文件或目录路径</param>
    /// <param name="archivePath">压缩文件保存路径</param>
    /// <param name="format">压缩格式</param>
    /// <param name="level">压缩级别</param>
    /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
    /// <exception cref="DirectoryNotFoundException">目录不存在时抛出</exception>
    public static void Compress(string sourcePath, string archivePath, CompressionFormat format = CompressionFormat.Zip, CompressionLevel level = CompressionLevel.Optimal)
    {
        if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
        {
            throw new FileNotFoundException("源文件或目录不存在。", sourcePath);
        }

        switch (format)
        {
            case CompressionFormat.Zip:
                if (Directory.Exists(sourcePath))
                {
                    ZipFile.CreateFromDirectory(sourcePath, archivePath, level, false);
                }
                else
                {
                    CompressFileToZip(sourcePath, archivePath, level);
                }
                break;

            case CompressionFormat.GZip:
                CompressToGZip(sourcePath, archivePath, level);
                break;

            case CompressionFormat.Deflate:
                CompressToDeflate(sourcePath, archivePath, level);
                break;

            default:
                throw new ArgumentException("不支持的压缩格式。", nameof(format));
        }
    }

    /// <summary>
    /// 压缩单个文件到ZIP
    /// </summary>
    private static void CompressFileToZip(string sourceFile, string archivePath, CompressionLevel level)
    {
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        archive.CreateEntryFromFile(sourceFile, Path.GetFileName(sourceFile), level);
    }

    /// <summary>
    /// 压缩到GZIP
    /// </summary>
    private static void CompressToGZip(string sourcePath, string archivePath, CompressionLevel level)
    {
        using var sourceStream = File.OpenRead(sourcePath);
        using var destinationStream = File.Create(archivePath);
        using var gzipStream = new GZipStream(destinationStream, level);
        sourceStream.CopyTo(gzipStream);
    }

    /// <summary>
    /// 压缩到DEFLATE
    /// </summary>
    private static void CompressToDeflate(string sourcePath, string archivePath, CompressionLevel level)
    {
        using var sourceStream = File.OpenRead(sourcePath);
        using var destinationStream = File.Create(archivePath);
        using var deflateStream = new DeflateStream(destinationStream, level);
        sourceStream.CopyTo(deflateStream);
    }

    /// <summary>
    /// 从GZIP解压
    /// </summary>
    private static void ExtractGZip(string archivePath, string extractPath)
    {
        using var sourceStream = File.OpenRead(archivePath);
        using var gzipStream = new GZipStream(sourceStream, CompressionMode.Decompress);
        using var destinationStream = File.Create(Path.Combine(extractPath, Path.GetFileNameWithoutExtension(archivePath)));
        gzipStream.CopyTo(destinationStream);
    }

    /// <summary>
    /// 从DEFLATE解压
    /// </summary>
    private static void ExtractDeflate(string archivePath, string extractPath)
    {
        using var sourceStream = File.OpenRead(archivePath);
        using var deflateStream = new DeflateStream(sourceStream, CompressionMode.Decompress);
        using var destinationStream = File.Create(Path.Combine(extractPath, Path.GetFileNameWithoutExtension(archivePath)));
        deflateStream.CopyTo(destinationStream);
    }
}

/// <summary>
/// 压缩格式
/// </summary>
public enum CompressionFormat
{
    /// <summary>
    /// ZIP格式
    /// </summary>
    Zip,

    /// <summary>
    /// GZIP格式
    /// </summary>
    GZip,

    /// <summary>
    /// DEFLATE格式
    /// </summary>
    Deflate
}
