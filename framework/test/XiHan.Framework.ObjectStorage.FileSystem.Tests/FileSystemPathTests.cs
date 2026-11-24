// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Abstractions.Listing;

namespace XiHan.Framework.ObjectStorage.FileSystem.Tests;

/// <summary>
/// 键到文件路径的映射与根路径（ADR-0213）
/// </summary>
/// <remarks>
/// Windows 把首个点之前是设备名的文件名都当作设备，编码要打破所有这样的段；根路径写不写结尾分隔符都一样能用
/// </remarks>
public sealed class FileSystemPathTests : IDisposable
{
    /// <summary>
    /// 本用例的临时根目录
    /// </summary>
    private readonly TemporaryRoot _root = new();

    /// <inheritdoc />
    public void Dispose() => _root.Dispose();

    [Fact]
    public async Task DeviceNames_AreEscaped_WhateverExtensionsFollow()
    {
        // 首个点之前是设备名就要编码：NUL.tar.gz 与 NUL 等价；COM0 与 LPT0 也是保留名
        Assert.Equal("%63on.tar.gz", BlobPathEncoding.EncodeSegment("con.tar.gz"));
        Assert.Equal("%6Eul.txt", BlobPathEncoding.EncodeSegment("nul.txt"));
        Assert.Equal("%63om0", BlobPathEncoding.EncodeSegment("com0"));
        Assert.Equal("%6Cpt0.log.1", BlobPathEncoding.EncodeSegment("lpt0.log.1"));
        // 设备名只是前缀、不在首个点之前的照常
        Assert.Equal("console.txt", BlobPathEncoding.EncodeSegment("console.txt"));
        Assert.Equal("a.con", BlobPathEncoding.EncodeSegment("a.con"));

        var container = new ContainerName("dev");
        var store = await CreateAsync(_root.Path);
        string[] keys = ["aux.tar.gz", "com0", "logs/lpt0.log.1", "nul.txt"];
        foreach (var key in keys)
        {
            Assert.True((await store.PutAsync(container, new BlobKey(key), Encoding.UTF8.GetBytes(key), BlobPutOptions.Default, CancellationToken.None)).IsSuccess, key);
        }

        foreach (var key in keys)
        {
            Assert.Equal(key, Encoding.UTF8.GetString((await store.ReadAllBytesAsync(container, new BlobKey(key), CancellationToken.None)).ValueOrThrow()));
        }

        Assert.Equal(keys, (await store.ListAsync(container, BlobQuery.All, CancellationToken.None)).ValueOrThrow().Items.Select(static m => m.Key.Value));
    }

    [Fact]
    public async Task RootPath_WithATrailingSeparator_IsNormalized_AndWorks()
    {
        var options = new FileSystemObjectStorageOptions { RootPath = _root.Path + Path.DirectorySeparatorChar };
        Assert.Equal(Path.GetFullPath(_root.Path), options.RootPath);

        var container = new ContainerName("docs");
        var store = await FileSystemBlobStore.CreateAsync(options, TimeProvider.System, NullLoggerFactory.Instance, CancellationToken.None);
        Assert.True((await store.PutAsync(container, new BlobKey("a/b.txt"), Encoding.UTF8.GetBytes("abc"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);
        Assert.Equal("abc", Encoding.UTF8.GetString((await store.ReadAllBytesAsync(container, new BlobKey("a/b.txt"), CancellationToken.None)).ValueOrThrow()));
        Assert.Equal(["a/b.txt"], (await store.ListAsync(container, BlobQuery.All, CancellationToken.None)).ValueOrThrow().Items.Select(static m => m.Key.Value));
    }

    [Fact]
    public void TheLongestKeys_EncodeToAtMostThreeCharactersPerByte_AndDecodeBack()
    {
        // 键的上限按 UTF-8 字节计（ADR-0282）：编码把要转义的字节各写成 %XX，相对路径最长是字节数的三倍。
        // 以前按 UTF-16 码元计，1024 个汉字的键合法，编出来是九倍
        string[] keys =
        [
            new string('K', BlobKey.MaxBytes),
            string.Join(BlobKey.Separator, Enumerable.Repeat(new string('键', 8), 41)),
            string.Concat(Enumerable.Repeat("😀", BlobKey.MaxBytes / 4)),
        ];
        foreach (var key in keys)
        {
            Assert.Equal(BlobKey.MaxBytes, Encoding.UTF8.GetByteCount(key));

            var path = BlobPathEncoding.ToRelativePath(new BlobKey(key));

            Assert.True(path.Length <= 3 * BlobKey.MaxBytes, $"The key of {key.Length} code units encodes to {path.Length} characters.");
            Assert.Equal(key, BlobPathEncoding.ToKey(path));
        }

        // 多一个字节的键构造不出来，到不了路径这一步
        Assert.Throws<ArgumentException>(static () => new BlobKey(new string('键', 341) + "ab"));
    }

    [Fact]
    public async Task AKeyOfTheMaximumBytes_InShortSegments_RoundTrips()
    {
        // 编码后的相对路径约 3000 个字符：Windows 与 Linux 容得下，macOS 的整条路径上限是 1024 字节
        Assert.SkipWhen(OperatingSystem.IsMacOS(), "The encoded path of a key of the maximum length exceeds the 1024-byte path limit of macOS.");

        // 41 段、每段 8 个汉字：UTF-8 恰好 1024 字节，每段编码后 72 个字符，不碰单个文件名的上限
        var key = new BlobKey(string.Join(BlobKey.Separator, Enumerable.Repeat(new string('键', 8), 41)));
        var container = new ContainerName("long");
        var store = await CreateAsync(_root.Path);

        Assert.True((await store.PutAsync(container, key, Encoding.UTF8.GetBytes("deep"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);

        Assert.Equal("deep", Encoding.UTF8.GetString((await store.ReadAllBytesAsync(container, key, CancellationToken.None)).ValueOrThrow()));
        Assert.Equal([key.Value], (await store.ListAsync(container, BlobQuery.All, CancellationToken.None)).ValueOrThrow().Items.Select(static m => m.Key.Value));
        Assert.True((await store.DeleteAsync(container, key, CancellationToken.None)).ValueOrThrow());
    }

    /// <summary>
    /// 在给定根目录上建出并初始化存储
    /// </summary>
    private static async Task<FileSystemBlobStore> CreateAsync(string root)
        => await FileSystemBlobStore.CreateAsync(new FileSystemObjectStorageOptions { RootPath = root }, TimeProvider.System, NullLoggerFactory.Instance, CancellationToken.None);
}
