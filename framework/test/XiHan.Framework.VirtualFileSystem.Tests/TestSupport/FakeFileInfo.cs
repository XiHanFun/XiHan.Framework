// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.FileProviders;
using System.Text;

namespace XiHan.Framework.VirtualFileSystem.Tests.TestSupport;

/// <summary>
/// 手写的文件信息替身
/// </summary>
/// <remarks>
/// 本仓测试栈不引入替身框架，所有假对象手写。这里刻意让每个属性都能独立赋值，
/// 用于验证包装器是原样透传元数据，而不是自己重新推导（例如 Length 必须来自元数据而非流长度）。
/// </remarks>
internal sealed class FakeFileInfo : IFileInfo
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">文件名</param>
    public FakeFileInfo(string name)
    {
        Name = name;
    }

    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 是否存在
    /// </summary>
    public bool Exists { get; init; }

    /// <summary>
    /// 文件长度
    /// </summary>
    public long Length { get; init; }

    /// <summary>
    /// 物理路径
    /// </summary>
    public string? PhysicalPath { get; init; }

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTimeOffset LastModified { get; init; }

    /// <summary>
    /// 是否为目录
    /// </summary>
    public bool IsDirectory { get; init; }

    /// <summary>
    /// 读取流返回的字节内容
    /// </summary>
    public byte[] Content { get; init; } = [];

    /// <summary>
    /// CreateReadStream 被调用的次数
    /// </summary>
    public int CreateReadStreamCallCount { get; private set; }

    /// <summary>
    /// 构造一个存在的文件替身，长度与内容一致
    /// </summary>
    /// <param name="name">文件名</param>
    /// <param name="content">文本内容</param>
    /// <returns>文件信息替身</returns>
    public static FakeFileInfo ForContent(string name, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new FakeFileInfo(name)
        {
            Exists = true,
            Content = bytes,
            Length = bytes.LongLength
        };
    }

    /// <summary>
    /// 构造一个目录替身
    /// </summary>
    /// <param name="name">目录名</param>
    /// <returns>文件信息替身</returns>
    public static FakeFileInfo ForDirectory(string name)
    {
        return new FakeFileInfo(name)
        {
            Exists = true,
            IsDirectory = true
        };
    }

    /// <summary>
    /// 创建读取流
    /// </summary>
    /// <returns>内容流</returns>
    public Stream CreateReadStream()
    {
        CreateReadStreamCallCount++;
        return new MemoryStream(Content, writable: false);
    }
}
