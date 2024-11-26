#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:StreamExtensions
// Guid:3d630fac-63f3-43e2-b8bb-0ee7c5f660a8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/23 19:08:09
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.IO;

/// <summary>
/// 文件流扩展方法
/// </summary>
public static class StreamExtensions
{
    /// <summary>
    /// 获取文件流字节
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    public static byte[] GetAllBytes(this Stream stream)
    {
        if (stream is MemoryStream memoryStream)
        {
            return memoryStream.ToArray();
        }

        using var ms = stream.CreateMemoryStream();
        return ms.ToArray();
    }

    /// <summary>
    /// 获取文件流字节，异步
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<byte[]> GetAllBytesAsync(this Stream stream, CancellationToken cancellationToken = default)
    {
        if (stream is MemoryStream memoryStream)
        {
            return memoryStream.ToArray();
        }

        using var ms = await stream.CreateMemoryStreamAsync(cancellationToken);
        return ms.ToArray();
    }

    /// <summary>
    /// 复制文件流，异步
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="destination"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static Task CopyToAsync(this Stream stream, Stream destination, CancellationToken cancellationToken)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        // 81920 已经是默认值，但是需要设置才能传递 cancellationToken
        return stream.CopyToAsync(destination, 81920, cancellationToken);
    }

    /// <summary>
    /// 创建内存流
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    public static MemoryStream CreateMemoryStream(this Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        MemoryStream? memoryStream = new();
        stream.CopyTo(memoryStream);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        memoryStream.Position = 0;
        return memoryStream;
    }

    /// <summary>
    /// 创建内存流，异步
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<MemoryStream> CreateMemoryStreamAsync(this Stream stream, CancellationToken cancellationToken = default)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        MemoryStream? memoryStream = new();
        await stream.CopyToAsync(memoryStream, cancellationToken);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        memoryStream.Position = 0;
        return memoryStream;
    }
}