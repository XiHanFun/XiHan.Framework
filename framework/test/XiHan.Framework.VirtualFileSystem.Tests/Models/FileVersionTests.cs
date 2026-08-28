// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.FileProviders;
using System.Security.Cryptography;
using System.Text;
using XiHan.Framework.VirtualFileSystem.Models;
using XiHan.Framework.VirtualFileSystem.Providers.Physical;

namespace XiHan.Framework.VirtualFileSystem.Tests;

/// <summary>
/// 文件版本快照测试
/// </summary>
/// <remarks>
/// 版本快照是回滚的唯一数据来源，必须做到：内容按字节完整复制、哈希由内容唯一决定、
/// 元数据（Length）取自 IFileInfo 而不是流长度。三者任意一条走样，回滚就会写坏文件。
/// </remarks>
public class FileVersionTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly VirtualPhysicalFileProvider _provider;

    /// <summary>
    /// 构造函数
    /// </summary>
    public FileVersionTests()
    {
        _provider = new VirtualPhysicalFileProvider(_temp.Root);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _provider.Dispose();
        _temp.Dispose();
    }

    /// <summary>
    /// 从真实物理文件构造时内容、长度与哈希都与磁盘一致
    /// </summary>
    [Fact]
    public void Constructor_FromPhysicalFile_CapturesContentLengthAndHash()
    {
        var path = _temp.WriteFile("data.txt", "hello 曦寒");
        var expectedBytes = File.ReadAllBytes(path);

        var version = new FileVersion(GetFileInfo("/data.txt"));

        Assert.Equal(expectedBytes, version.Content);
        Assert.Equal(expectedBytes.LongLength, version.Length);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(expectedBytes)), version.ContentHash);
    }

    /// <summary>
    /// 内容哈希是 SHA256 的十六进制串
    /// </summary>
    /// <remarks>
    /// 长度固定 64 位十六进制，锁住算法族——换成 MD5 会让已有快照的哈希语义静默改变。
    /// </remarks>
    [Fact]
    public void ContentHash_IsSha256HexString()
    {
        _temp.WriteFile("data.txt", "abc");

        var version = new FileVersion(GetFileInfo("/data.txt"));

        Assert.Equal(64, version.ContentHash.Length);
        Assert.Matches("^[0-9A-F]{64}$", version.ContentHash);
    }

    /// <summary>
    /// 长度取自文件元数据，而不是重新数流里的字节
    /// </summary>
    /// <remarks>
    /// 用一个元数据与内容故意不一致的替身来区分这两种实现：若改成按流长度计算，本用例会失败。
    /// </remarks>
    [Fact]
    public void Length_ComesFromFileMetadataNotStream()
    {
        var file = new FakeFileInfo("ghost.txt")
        {
            Exists = true,
            Length = 999,
            Content = Encoding.UTF8.GetBytes("abc")
        };

        var version = new FileVersion(file);

        Assert.Equal(999L, version.Length);
        Assert.Equal(3, version.Content.Length);
        Assert.Equal(1, file.CreateReadStreamCallCount);
    }

    /// <summary>
    /// 内容相同则哈希相同，内容不同则哈希不同
    /// </summary>
    [Fact]
    public void ContentHash_IsDeterminedByContentOnly()
    {
        var same1 = new FileVersion(FakeFileInfo.ForContent("a.txt", "same"));
        var same2 = new FileVersion(FakeFileInfo.ForContent("b.txt", "same"));
        var other = new FileVersion(FakeFileInfo.ForContent("a.txt", "other"));

        Assert.Equal(same1.ContentHash, same2.ContentHash);
        Assert.NotEqual(same1.ContentHash, other.ContentHash);
    }

    /// <summary>
    /// 空内容也能生成快照，内容为空数组、哈希为空串的 SHA256
    /// </summary>
    [Fact]
    public void Constructor_ForEmptyContent_ProducesEmptyBufferAndHash()
    {
        var version = new FileVersion(FakeFileInfo.ForContent("empty.txt", string.Empty));

        Assert.Empty(version.Content);
        Assert.Equal(Convert.ToHexString(SHA256.HashData([])), version.ContentHash);
    }

    /// <summary>
    /// 时间戳取当前 UTC 时刻
    /// </summary>
    [Fact]
    public void Timestamp_IsCurrentUtcMoment()
    {
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);

        var version = new FileVersion(FakeFileInfo.ForContent("a.txt", "x"));

        Assert.InRange(version.Timestamp, before, DateTimeOffset.UtcNow.AddMinutes(1));
    }

    private IFileInfo GetFileInfo(string subpath)
    {
        return ((IFileProvider)_provider).GetFileInfo(subpath);
    }
}
