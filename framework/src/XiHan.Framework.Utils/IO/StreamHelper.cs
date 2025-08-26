#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:StreamHelper
// Guid:8b6c9d3e-5f7a-4e2b-9c1d-8a7f6e5d4c3b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/19 13:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace XiHan.Framework.Utils.IO;

/// <summary>
/// 流操作辅助工具类
/// </summary>
/// <remarks>
/// 提供全面的流操作功能，包括基本读写、异步操作、内存流处理、
/// 压缩操作、进度监控等功能
/// </remarks>
public static class StreamHelper
{
    #region 常量定义

    /// <summary>
    /// 默认缓冲区大小
    /// </summary>
    private const int DefaultBufferSize = 8192;

    /// <summary>
    /// 大文件缓冲区大小
    /// </summary>
    private const int LargeBufferSize = 65536;

    /// <summary>
    /// 默认编码
    /// </summary>
    private static readonly Encoding DefaultEncoding = Encoding.UTF8;

    #endregion

    #region 基本流操作

    /// <summary>
    /// 读取流中的所有字节
    /// </summary>
    /// <param name="stream">源流</param>
    /// <returns>字节数组</returns>
    /// <exception cref="ArgumentNullException">流为null时抛出</exception>
    public static byte[] ReadAllBytes(Stream stream)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (stream is MemoryStream memoryStream)
        {
            return memoryStream.ToArray();
        }

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// 异步读取流中的所有字节
    /// </summary>
    /// <param name="stream">源流</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>字节数组</returns>
    /// <exception cref="ArgumentNullException">流为null时抛出</exception>
    public static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (stream is MemoryStream memoryStream)
        {
            return memoryStream.ToArray();
        }

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }

    /// <summary>
    /// 读取流中的所有文本
    /// </summary>
    /// <param name="stream">源流</param>
    /// <param name="encoding">编码格式</param>
    /// <returns>文本内容</returns>
    /// <exception cref="ArgumentNullException">流为null时抛出</exception>
    public static string ReadAllText(Stream stream, Encoding? encoding = null)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        encoding ??= DefaultEncoding;
        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// 异步读取流中的所有文本
    /// </summary>
    /// <param name="stream">源流</param>
    /// <param name="encoding">编码格式</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文本内容</returns>
    /// <exception cref="ArgumentNullException">流为null时抛出</exception>
    public static async Task<string> ReadAllTextAsync(Stream stream, Encoding? encoding = null, CancellationToken cancellationToken = default)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        encoding ??= DefaultEncoding;
        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    /// <summary>
    /// 读取流中的所有行
    /// </summary>
    /// <param name="stream">源流</param>
    /// <param name="encoding">编码格式</param>
    /// <returns>行数组</returns>
    /// <exception cref="ArgumentNullException">流为null时抛出</exception>
    public static string[] ReadAllLines(Stream stream, Encoding? encoding = null)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        encoding ??= DefaultEncoding;
        var lines = new List<string>();
        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true, leaveOpen: true);

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            lines.Add(line);
        }

        return [.. lines];
    }

    /// <summary>
    /// 异步读取流中的所有行
    /// </summary>
    /// <param name="stream">源流</param>
    /// <param name="encoding">编码格式</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>行数组</returns>
    /// <exception cref="ArgumentNullException">流为null时抛出</exception>
    public static async Task<string[]> ReadAllLinesAsync(Stream stream, Encoding? encoding = null, CancellationToken cancellationToken = default)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        encoding ??= DefaultEncoding;
        var lines = new List<string>();
        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true, leaveOpen: true);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            lines.Add(line);
        }

        return [.. lines];
    }

    /// <summary>
    /// 将字节数组写入流
    /// </summary>
    /// <param name="stream">目标流</param>
    /// <param name="buffer">字节数组</param>
    /// <exception cref="ArgumentNullException">参数为null时抛出</exception>
    public static void WriteAllBytes(Stream stream, byte[] buffer)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        stream.Write(buffer, 0, buffer.Length);
    }

    /// <summary>
    /// 异步将字节数组写入流
    /// </summary>
    /// <param name="stream">目标流</param>
    /// <param name="buffer">字节数组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <exception cref="ArgumentNullException">参数为null时抛出</exception>
    public static async Task WriteAllBytesAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken = default)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        await stream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
    }

    /// <summary>
    /// 将文本写入流
    /// </summary>
    /// <param name="stream">目标流</param>
    /// <param name="text">文本内容</param>
    /// <param name="encoding">编码格式</param>
    /// <exception cref="ArgumentNullException">参数为null时抛出</exception>
    public static void WriteAllText(Stream stream, string text, Encoding? encoding = null)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (text == null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        encoding ??= DefaultEncoding;
        using var writer = new StreamWriter(stream, encoding, leaveOpen: true);
        writer.Write(text);
        writer.Flush();
    }

    /// <summary>
    /// 异步将文本写入流
    /// </summary>
    /// <param name="stream">目标流</param>
    /// <param name="text">文本内容</param>
    /// <param name="encoding">编码格式</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <exception cref="ArgumentNullException">参数为null时抛出</exception>
    public static async Task WriteAllTextAsync(Stream stream, string text, Encoding? encoding = null, CancellationToken cancellationToken = default)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (text == null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        encoding ??= DefaultEncoding;
        using var writer = new StreamWriter(stream, encoding, leaveOpen: true);
        await writer.WriteAsync(text.AsMemory(), cancellationToken);
        await writer.FlushAsync(cancellationToken);
    }

    #endregion

    #region 流拷贝操作

    /// <summary>
    /// 拷贝流数据
    /// </summary>
    /// <param name="source">源流</param>
    /// <param name="destination">目标流</param>
    /// <param name="bufferSize">缓冲区大小</param>
    /// <returns>拷贝的字节数</returns>
    /// <exception cref="ArgumentNullException">参数为null时抛出</exception>
    public static long CopyTo(Stream source, Stream destination, int bufferSize = DefaultBufferSize)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        var buffer = new byte[bufferSize];
        long totalBytes = 0;
        int bytesRead;

        while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            destination.Write(buffer, 0, bytesRead);
            totalBytes += bytesRead;
        }

        return totalBytes;
    }

    /// <summary>
    /// 异步拷贝流数据
    /// </summary>
    /// <param name="source">源流</param>
    /// <param name="destination">目标流</param>
    /// <param name="bufferSize">缓冲区大小</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>拷贝的字节数</returns>
    /// <exception cref="ArgumentNullException">参数为null时抛出</exception>
    public static async Task<long> CopyToAsync(Stream source, Stream destination, int bufferSize = DefaultBufferSize, CancellationToken cancellationToken = default)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        var buffer = new byte[bufferSize];
        long totalBytes = 0;
        int bytesRead;

        while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            totalBytes += bytesRead;
        }

        return totalBytes;
    }

    /// <summary>
    /// 带进度报告的流拷贝
    /// </summary>
    /// <param name="source">源流</param>
    /// <param name="destination">目标流</param>
    /// <param name="progress">进度报告</param>
    /// <param name="bufferSize">缓冲区大小</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>拷贝的字节数</returns>
    /// <exception cref="ArgumentNullException">参数为null时抛出</exception>
    public static async Task<long> CopyToWithProgressAsync(
        Stream source,
        Stream destination,
        IProgress<StreamCopyProgress>? progress = null,
        int bufferSize = DefaultBufferSize,
        CancellationToken cancellationToken = default)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        var buffer = new byte[bufferSize];
        long totalBytes = 0;
        long totalLength = source.CanSeek ? source.Length : -1;
        var stopwatch = Stopwatch.StartNew();
        int bytesRead;

        while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            totalBytes += bytesRead;

            progress?.Report(new StreamCopyProgress
            {
                BytesCopied = totalBytes,
                TotalBytes = totalLength,
                ElapsedTime = stopwatch.Elapsed,
                Speed = totalBytes / stopwatch.Elapsed.TotalSeconds
            });
        }

        stopwatch.Stop();
        return totalBytes;
    }

    #endregion

    #region 内存流操作

    /// <summary>
    /// 从字节数组创建内存流
    /// </summary>
    /// <param name="buffer">字节数组</param>
    /// <param name="writable">是否可写</param>
    /// <returns>内存流</returns>
    /// <exception cref="ArgumentNullException">字节数组为null时抛出</exception>
    public static MemoryStream CreateMemoryStream(byte[] buffer, bool writable = true)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        return new MemoryStream(buffer, writable);
    }

    /// <summary>
    /// 从字符串创建内存流
    /// </summary>
    /// <param name="text">文本内容</param>
    /// <param name="encoding">编码格式</param>
    /// <returns>内存流</returns>
    /// <exception cref="ArgumentNullException">文本为null时抛出</exception>
    public static MemoryStream CreateMemoryStreamFromText(string text, Encoding? encoding = null)
    {
        if (text == null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        encoding ??= DefaultEncoding;
        var bytes = encoding.GetBytes(text);
        return new MemoryStream(bytes);
    }

    /// <summary>
    /// 将流转换为字节数组
    /// </summary>
    /// <param name="stream">源流</param>
    /// <returns>字节数组</returns>
    /// <exception cref="ArgumentNullException">流为null时抛出</exception>
    public static byte[] ToByteArray(Stream stream)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (stream is MemoryStream memoryStream)
        {
            return memoryStream.ToArray();
        }

        var originalPosition = stream.CanSeek ? stream.Position : 0;
        try
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            return ReadAllBytes(stream);
        }
        finally
        {
            if (stream.CanSeek)
            {
                stream.Position = originalPosition;
            }
        }
    }

    /// <summary>
    /// 将字节数组转换为Base64字符串
    /// </summary>
    /// <param name="stream">源流</param>
    /// <returns>Base64字符串</returns>
    /// <exception cref="ArgumentNullException">流为null时抛出</exception>
    public static string ToBase64String(Stream stream)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        var bytes = ToByteArray(stream);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// 从Base64字符串创建内存流
    /// </summary>
    /// <param name="base64String">Base64字符串</param>
    /// <returns>内存流</returns>
    /// <exception cref="ArgumentNullException">字符串为null时抛出</exception>
    public static MemoryStream FromBase64String(string base64String)
    {
        if (base64String == null)
        {
            throw new ArgumentNullException(nameof(base64String));
        }

        var bytes = Convert.FromBase64String(base64String);
        return new MemoryStream(bytes);
    }

    #endregion

    #region 压缩操作

    /// <summary>
    /// 使用GZip压缩流
    /// </summary>
    /// <param name="source">源流</param>
    /// <param name="destination">目标流</param>
    /// <param name="level">压缩级别</param>
    /// <exception cref="ArgumentNullException">参数为null时抛出</exception>
    public static void CompressGZip(Stream source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        using var gzipStream = new GZipStream(destination, level, leaveOpen: true);
        source.CopyTo(gzipStream);
    }

    /// <summary>
    /// 异步使用GZip压缩流
    /// </summary>
    /// <param name="source">源流</param>
    /// <param name="destination">目标流</param>
    /// <param name="level">压缩级别</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <exception cref="ArgumentNullException">参数为null时抛出</exception>
    public static async Task CompressGZipAsync(Stream source, Stream destination, CompressionLevel level = CompressionLevel.Optimal, CancellationToken cancellationToken = default)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        using var gzipStream = new GZipStream(destination, level, leaveOpen: true);
        await source.CopyToAsync(gzipStream, cancellationToken);
    }

    /// <summary>
    /// 使用GZip解压缩流
    /// </summary>
    /// <param name="source">源流</param>
    /// <param name="destination">目标流</param>
    /// <exception cref="ArgumentNullException">参数为null时抛出</exception>
    public static void DecompressGZip(Stream source, Stream destination)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        using var gzipStream = new GZipStream(source, CompressionMode.Decompress, leaveOpen: true);
        gzipStream.CopyTo(destination);
    }

    /// <summary>
    /// 异步使用GZip解压缩流
    /// </summary>
    /// <param name="source">源流</param>
    /// <param name="destination">目标流</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <exception cref="ArgumentNullException">参数为null时抛出</exception>
    public static async Task DecompressGZipAsync(Stream source, Stream destination, CancellationToken cancellationToken = default)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        using var gzipStream = new GZipStream(source, CompressionMode.Decompress, leaveOpen: true);
        await gzipStream.CopyToAsync(destination, cancellationToken);
    }

    /// <summary>
    /// 压缩字节数组
    /// </summary>
    /// <param name="data">要压缩的数据</param>
    /// <param name="level">压缩级别</param>
    /// <returns>压缩后的字节数组</returns>
    /// <exception cref="ArgumentNullException">数据为null时抛出</exception>
    public static byte[] CompressBytes(byte[] data, CompressionLevel level = CompressionLevel.Optimal)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        using var sourceStream = new MemoryStream(data);
        using var destinationStream = new MemoryStream();
        CompressGZip(sourceStream, destinationStream, level);
        return destinationStream.ToArray();
    }

    /// <summary>
    /// 解压缩字节数组
    /// </summary>
    /// <param name="compressedData">压缩的数据</param>
    /// <returns>解压缩后的字节数组</returns>
    /// <exception cref="ArgumentNullException">数据为null时抛出</exception>
    public static byte[] DecompressBytes(byte[] compressedData)
    {
        if (compressedData == null)
        {
            throw new ArgumentNullException(nameof(compressedData));
        }

        using var sourceStream = new MemoryStream(compressedData);
        using var destinationStream = new MemoryStream();
        DecompressGZip(sourceStream, destinationStream);
        return destinationStream.ToArray();
    }

    #endregion

    #region 流验证和分析

    /// <summary>
    /// 计算流的哈希值
    /// </summary>
    /// <param name="stream">源流</param>
    /// <param name="algorithm">哈希算法</param>
    /// <returns>哈希值的十六进制字符串</returns>
    /// <exception cref="ArgumentNullException">参数为null时抛出</exception>
    public static string ComputeHash(Stream stream, HashAlgorithm algorithm)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (algorithm == null)
        {
            throw new ArgumentNullException(nameof(algorithm));
        }

        var originalPosition = stream.CanSeek ? stream.Position : 0;
        try
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            var hash = algorithm.ComputeHash(stream);
            return Convert.ToHexString(hash);
        }
        finally
        {
            if (stream.CanSeek)
            {
                stream.Position = originalPosition;
            }
        }
    }

    /// <summary>
    /// 计算流的MD5哈希值
    /// </summary>
    /// <param name="stream">源流</param>
    /// <returns>MD5哈希值</returns>
    /// <exception cref="ArgumentNullException">流为null时抛出</exception>
    public static string ComputeMD5Hash(Stream stream)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        using var md5 = MD5.Create();
        return ComputeHash(stream, md5);
    }

    /// <summary>
    /// 计算流的SHA256哈希值
    /// </summary>
    /// <param name="stream">源流</param>
    /// <returns>SHA256哈希值</returns>
    /// <exception cref="ArgumentNullException">流为null时抛出</exception>
    public static string ComputeSHA256Hash(Stream stream)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        using var sha256 = SHA256.Create();
        return ComputeHash(stream, sha256);
    }

    /// <summary>
    /// 比较两个流的内容是否相同
    /// </summary>
    /// <param name="stream1">流1</param>
    /// <param name="stream2">流2</param>
    /// <param name="bufferSize">缓冲区大小</param>
    /// <returns>是否相同</returns>
    /// <exception cref="ArgumentNullException">参数为null时抛出</exception>
    public static bool CompareStreams(Stream stream1, Stream stream2, int bufferSize = DefaultBufferSize)
    {
        if (stream1 == null)
        {
            throw new ArgumentNullException(nameof(stream1));
        }

        if (stream2 == null)
        {
            throw new ArgumentNullException(nameof(stream2));
        }

        if (stream1.CanSeek && stream2.CanSeek && stream1.Length != stream2.Length)
        {
            return false;
        }

        var buffer1 = new byte[bufferSize];
        var buffer2 = new byte[bufferSize];

        while (true)
        {
            var bytesRead1 = stream1.Read(buffer1, 0, bufferSize);
            var bytesRead2 = stream2.Read(buffer2, 0, bufferSize);

            if (bytesRead1 != bytesRead2)
            {
                return false;
            }

            if (bytesRead1 == 0)
            {
                return true;
            }

            for (var i = 0; i < bytesRead1; i++)
            {
                if (buffer1[i] != buffer2[i])
                {
                    return false;
                }
            }
        }
    }

    /// <summary>
    /// 异步比较两个流的内容是否相同
    /// </summary>
    /// <param name="stream1">流1</param>
    /// <param name="stream2">流2</param>
    /// <param name="bufferSize">缓冲区大小</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否相同</returns>
    /// <exception cref="ArgumentNullException">参数为null时抛出</exception>
    public static async Task<bool> CompareStreamsAsync(Stream stream1, Stream stream2, int bufferSize = DefaultBufferSize, CancellationToken cancellationToken = default)
    {
        if (stream1 == null)
        {
            throw new ArgumentNullException(nameof(stream1));
        }

        if (stream2 == null)
        {
            throw new ArgumentNullException(nameof(stream2));
        }

        if (stream1.CanSeek && stream2.CanSeek && stream1.Length != stream2.Length)
        {
            return false;
        }

        var buffer1 = new byte[bufferSize];
        var buffer2 = new byte[bufferSize];

        while (true)
        {
            var bytesRead1 = await stream1.ReadAsync(buffer1, 0, bufferSize, cancellationToken);
            var bytesRead2 = await stream2.ReadAsync(buffer2, 0, bufferSize, cancellationToken);

            if (bytesRead1 != bytesRead2)
            {
                return false;
            }

            if (bytesRead1 == 0)
            {
                return true;
            }

            for (var i = 0; i < bytesRead1; i++)
            {
                if (buffer1[i] != buffer2[i])
                {
                    return false;
                }
            }
        }
    }

    /// <summary>
    /// 获取流的基本信息
    /// </summary>
    /// <param name="stream">流</param>
    /// <returns>流信息</returns>
    /// <exception cref="ArgumentNullException">流为null时抛出</exception>
    public static StreamInfo GetStreamInfo(Stream stream)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        return new StreamInfo
        {
            Length = stream.CanSeek ? stream.Length : -1,
            Position = stream.CanSeek ? stream.Position : -1,
            CanRead = stream.CanRead,
            CanWrite = stream.CanWrite,
            CanSeek = stream.CanSeek,
            CanTimeout = stream.CanTimeout,
            StreamType = stream.GetType().Name,
            ReadTimeout = stream.CanTimeout ? stream.ReadTimeout : -1,
            WriteTimeout = stream.CanTimeout ? stream.WriteTimeout : -1
        };
    }

    #endregion

    #region 文件流操作

    /// <summary>
    /// 创建文件流用于读取
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="bufferSize">缓冲区大小</param>
    /// <returns>文件流</returns>
    /// <exception cref="ArgumentException">文件路径为空时抛出</exception>
    public static FileStream CreateFileStreamForRead(string filePath, int bufferSize = DefaultBufferSize)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("文件路径不能为空", nameof(filePath));
        }

        return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan);
    }

    /// <summary>
    /// 创建文件流用于写入
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="bufferSize">缓冲区大小</param>
    /// <param name="overwrite">是否覆盖现有文件</param>
    /// <returns>文件流</returns>
    /// <exception cref="ArgumentException">文件路径为空时抛出</exception>
    public static FileStream CreateFileStreamForWrite(string filePath, int bufferSize = DefaultBufferSize, bool overwrite = true)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("文件路径不能为空", nameof(filePath));
        }

        var mode = overwrite ? FileMode.Create : FileMode.CreateNew;
        return new FileStream(filePath, mode, FileAccess.Write, FileShare.None, bufferSize, FileOptions.SequentialScan);
    }

    /// <summary>
    /// 将流保存到文件
    /// </summary>
    /// <param name="stream">源流</param>
    /// <param name="filePath">文件路径</param>
    /// <param name="bufferSize">缓冲区大小</param>
    /// <exception cref="ArgumentNullException">流为null时抛出</exception>
    /// <exception cref="ArgumentException">文件路径为空时抛出</exception>
    public static void SaveToFile(Stream stream, string filePath, int bufferSize = DefaultBufferSize)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("文件路径不能为空", nameof(filePath));
        }

        using var fileStream = CreateFileStreamForWrite(filePath, bufferSize);
        stream.CopyTo(fileStream, bufferSize);
    }

    /// <summary>
    /// 异步将流保存到文件
    /// </summary>
    /// <param name="stream">源流</param>
    /// <param name="filePath">文件路径</param>
    /// <param name="bufferSize">缓冲区大小</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <exception cref="ArgumentNullException">流为null时抛出</exception>
    /// <exception cref="ArgumentException">文件路径为空时抛出</exception>
    public static async Task SaveToFileAsync(Stream stream, string filePath, int bufferSize = DefaultBufferSize, CancellationToken cancellationToken = default)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("文件路径不能为空", nameof(filePath));
        }

        using var fileStream = CreateFileStreamForWrite(filePath, bufferSize);
        await stream.CopyToAsync(fileStream, bufferSize, cancellationToken);
    }

    #endregion

    #region 实用工具方法

    /// <summary>
    /// 获取建议的缓冲区大小
    /// </summary>
    /// <param name="streamLength">流长度</param>
    /// <returns>建议的缓冲区大小</returns>
    public static int GetSuggestedBufferSize(long streamLength)
    {
        return streamLength switch
        {
            <= 0 => DefaultBufferSize,
            <= 1024 * 1024 => DefaultBufferSize, // 1MB以下使用默认缓冲区
            <= 100 * 1024 * 1024 => DefaultBufferSize * 2, // 100MB以下使用16KB缓冲区
            _ => LargeBufferSize // 大文件使用64KB缓冲区
        };
    }

    /// <summary>
    /// 检查流是否为空
    /// </summary>
    /// <param name="stream">流</param>
    /// <returns>是否为空</returns>
    /// <exception cref="ArgumentNullException">流为null时抛出</exception>
    public static bool IsEmpty(Stream stream)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (stream.CanSeek)
        {
            return stream.Length == 0;
        }

        // 对于不可定位的流，尝试读取一个字节
        var originalPosition = stream.Position;
        try
        {
            return stream.ReadByte() == -1;
        }
        finally
        {
            if (stream.CanSeek)
            {
                stream.Position = originalPosition;
            }
        }
    }

    /// <summary>
    /// 安全地重置流位置到开始
    /// </summary>
    /// <param name="stream">流</param>
    /// <returns>是否成功重置</returns>
    /// <exception cref="ArgumentNullException">流为null时抛出</exception>
    public static bool TrySeekToBeginning(Stream stream)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (!stream.CanSeek)
        {
            return false;
        }

        try
        {
            stream.Position = 0;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 创建只读流包装器
    /// </summary>
    /// <param name="stream">源流</param>
    /// <returns>只读流</returns>
    /// <exception cref="ArgumentNullException">流为null时抛出</exception>
    public static Stream CreateReadOnlyWrapper(Stream stream)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        return new ReadOnlyStreamWrapper(stream);
    }

    #endregion
}

/// <summary>
/// 流拷贝进度信息
/// </summary>
public class StreamCopyProgress
{
    /// <summary>
    /// 已拷贝的字节数
    /// </summary>
    public long BytesCopied { get; set; }

    /// <summary>
    /// 总字节数（-1表示未知）
    /// </summary>
    public long TotalBytes { get; set; }

    /// <summary>
    /// 已用时间
    /// </summary>
    public TimeSpan ElapsedTime { get; set; }

    /// <summary>
    /// 传输速度（字节/秒）
    /// </summary>
    public double Speed { get; set; }

    /// <summary>
    /// 完成百分比（0-100，-1表示未知）
    /// </summary>
    public double PercentageComplete => TotalBytes > 0 ? (double)BytesCopied / TotalBytes * 100 : -1;

    /// <summary>
    /// 预估剩余时间
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining
    {
        get
        {
            if (TotalBytes <= 0 || Speed <= 0)
            {
                return null;
            }

            var remainingBytes = TotalBytes - BytesCopied;
            return TimeSpan.FromSeconds(remainingBytes / Speed);
        }
    }
}

/// <summary>
/// 流基本信息
/// </summary>
public class StreamInfo
{
    /// <summary>
    /// 流长度
    /// </summary>
    public long Length { get; set; }

    /// <summary>
    /// 当前位置
    /// </summary>
    public long Position { get; set; }

    /// <summary>
    /// 是否可读
    /// </summary>
    public bool CanRead { get; set; }

    /// <summary>
    /// 是否可写
    /// </summary>
    public bool CanWrite { get; set; }

    /// <summary>
    /// 是否可定位
    /// </summary>
    public bool CanSeek { get; set; }

    /// <summary>
    /// 是否支持超时
    /// </summary>
    public bool CanTimeout { get; set; }

    /// <summary>
    /// 流类型名称
    /// </summary>
    public string StreamType { get; set; } = string.Empty;

    /// <summary>
    /// 读取超时时间
    /// </summary>
    public int ReadTimeout { get; set; }

    /// <summary>
    /// 写入超时时间
    /// </summary>
    public int WriteTimeout { get; set; }

    /// <summary>
    /// 转换为字符串表示
    /// </summary>
    /// <returns>格式化的流信息</returns>
    public override string ToString()
    {
        return $"{StreamType}: Length={Length}, Position={Position}, CanRead={CanRead}, CanWrite={CanWrite}, CanSeek={CanSeek}";
    }
}

/// <summary>
/// 只读流包装器
/// </summary>
internal class ReadOnlyStreamWrapper : Stream
{
    private readonly Stream _baseStream;

    public ReadOnlyStreamWrapper(Stream baseStream)
    {
        _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
    }

    public override bool CanRead => _baseStream.CanRead;
    public override bool CanSeek => _baseStream.CanSeek;
    public override bool CanWrite => false;
    public override long Length => _baseStream.Length;

    public override long Position
    {
        get => _baseStream.Position;
        set => _baseStream.Position = value;
    }

    public override void Flush()
    {
        // 只读流不需要刷新
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return _baseStream.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return _baseStream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException("只读流不支持设置长度");
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException("只读流不支持写入");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _baseStream.Dispose();
        }
        base.Dispose(disposing);
    }
}
