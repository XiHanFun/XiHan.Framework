// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.Testing;

namespace XiHan.Framework.ObjectStorage.FileSystem.Tests;

/// <summary>
/// Windows 上以 POSIX 语义替换改名，以及卷不支持时的回退（ADR-0241）
/// </summary>
/// <remarks>
/// 只在 Windows 上运行，其他平台上每个用例先跳过：其他平台的 rename(2) 本来就是这个语义，不经它
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class PosixRenameTests : IDisposable
{
    /// <summary>
    /// 本用例的临时根目录
    /// </summary>
    private readonly TemporaryRoot _root = new();

    /// <inheritdoc />
    public void Dispose() => _root.Dispose();

    [Fact]
    public void TryReplace_WhileTheTargetIsOpenWithDeleteSharing_ReplacesIt_AndTheReaderKeepsTheOldContent()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("POSIX rename semantics are only requested explicitly on Windows.");
            return;
        }

        var target = Write("target", "old");
        var source = Write("target.tmp", "new");
        // 普通的替换改名（File.Move 带 overwrite）在这里以拒绝访问失败，带 FileShare.Delete 也一样（本机实测）
        using var reader = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);

        Assert.True(PosixRename.TryReplace(source, target));

        Assert.Equal("new", File.ReadAllText(target));
        Assert.False(File.Exists(source));
        using var old = new StreamReader(reader);
        Assert.Equal("old", old.ReadToEnd());
    }

    [Fact]
    public void TryReplace_WhileTheTargetIsOpenWithoutDeleteSharing_FailsWithASharingViolation_AndLeavesBothFiles()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("POSIX rename semantics are only requested explicitly on Windows.");
            return;
        }

        var target = Write("target", "old");
        var source = Write("target.tmp", "new");
        using (new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var exception = Assert.ThrowsAny<IOException>(() => PosixRename.TryReplace(source, target));

            Assert.Equal(unchecked((int)0x80070020), exception.HResult);
        }

        Assert.Equal("old", File.ReadAllText(target));
        Assert.Equal("new", File.ReadAllText(source));
    }

    [Fact]
    public void TryReplace_PathsLongerThanMaxPath_AreReplaced()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("POSIX rename semantics are only requested explicitly on Windows.");
            return;
        }

        // 改名经句柄进行，不经 .NET 的路径处理：不加扩展长度前缀时超过 260 个字符的路径打不开
        var directory = Path.Combine(_root.Path, new string('d', 120), new string('e', 120));
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, "target");
        var source = target + ".tmp";
        File.WriteAllText(target, "old");
        File.WriteAllText(source, "new");
        Assert.True(target.Length > 260);

        Assert.True(PosixRename.TryReplace(source, target));

        Assert.Equal("new", File.ReadAllText(target));
        Assert.False(File.Exists(source));
    }

    [Fact]
    public void TryReplace_WithoutTheSource_ThrowsFileNotFound()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("POSIX rename semantics are only requested explicitly on Windows.");
            return;
        }

        var target = Write("target", "old");

        Assert.Throws<FileNotFoundException>(() => PosixRename.TryReplace(Path.Combine(_root.Path, "missing"), target));
        Assert.Equal("old", File.ReadAllText(target));
    }

    [Fact]
    public async Task Store_OnAVolumeWithoutPosixRename_FallsBackToThePlainReplace_AndWarnsOnce()
    {
        // SMB 共享不认 FileRenameInfoEx（本机实测 ERROR_INVALID_PARAMETER）：经本机的管理共享访问同一个临时目录，够不着就跳过
        if (!OperatingSystem.IsWindows() || _root.Path.Length < 3 || _root.Path[1] != ':')
        {
            Assert.Skip("Needs a Windows drive path that can be reached through the loopback administrative share.");
            return;
        }

        var share = $@"\\localhost\{_root.Path[0]}${_root.Path[2..]}";
        if (!Directory.Exists(share))
        {
            Assert.Skip($"The administrative share '{share}' is not reachable.");
            return;
        }

        var probe = Path.Combine(share, "probe");
        File.WriteAllText(probe, "old");
        File.WriteAllText(probe + ".tmp", "new");
        Assert.False(PosixRename.TryReplace(probe + ".tmp", probe));
        Assert.Equal("old", File.ReadAllText(probe));

        var logs = new LogCapture();
        var store = new FileSystemBlobStore(new FileSystemObjectStorageOptions { RootPath = Path.Combine(share, "blobs") }, new FakeTimeProvider(), logs);
        var container = new ContainerName("smb");
        var key = new BlobKey("a.txt");
        for (var round = 0; round < 3; round++)
        {
            var written = await store.PutAsync(container, key, new byte[(round + 1) * 100], BlobPutOptions.Default, CancellationToken.None);

            Assert.True(written.IsSuccess, written.Error?.ToString());
        }

        Assert.Equal(300, (await store.HeadAsync(container, key, CancellationToken.None)).ValueOrThrow().Length);
        Assert.Single(logs.Entries, static entry => entry.Level == LogLevel.Warning && entry.Message.Contains("POSIX semantics", StringComparison.Ordinal));
    }

    /// <summary>
    /// 在临时根目录里写一个文本文件
    /// </summary>
    private string Write(string name, string text)
    {
        var path = Path.Combine(_root.Path, name);
        File.WriteAllText(path, text);
        return path;
    }
}
