// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.FileProviders;
using XiHan.Framework.VirtualFileSystem.Providers.Physical;
using XiHan.Framework.VirtualFileSystem.Services;

namespace XiHan.Framework.VirtualFileSystem.Tests;

/// <summary>
/// 文件版本控制服务测试
/// </summary>
/// <remarks>
/// 版本按物理路径分栈存放，Rollback(steps) 是「连续弹出 steps 个版本、写回最后弹出的那个」，
/// 即回滚会消费版本栈。这里把累加、按步回滚、栈耗尽、目标文件消失几条路径都钉死，
/// 因为这些分支一旦走样，用户会拿到一个既不是旧版本也不是新版本的文件。
/// 快照的键取自 IFileInfo.PhysicalPath，用例统一用它作为回滚入参，避免字符串大小写/分隔符差异带来的假失败。
/// </remarks>
public class FileVersioningServiceTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FileVersioningService _sut = new();
    private readonly VirtualPhysicalFileProvider _provider;

    /// <summary>
    /// 构造函数
    /// </summary>
    public FileVersioningServiceTests()
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
    /// 没有物理路径的文件无法快照
    /// </summary>
    /// <remarks>
    /// 版本栈以物理路径为键，纯虚拟文件（如嵌入资源）没有可回写的目标，必须挡在入口。
    /// </remarks>
    [Fact]
    public void Snapshot_WhenPhysicalPathIsNull_Throws()
    {
        var file = new FakeFileInfo("virtual.txt") { Exists = true };

        var exception = Assert.Throws<ArgumentNullException>(() => _sut.Snapshot(file));
        Assert.Equal("file", exception.ParamName);
    }

    /// <summary>
    /// 单次快照后回滚恢复到快照时的内容
    /// </summary>
    [Fact]
    public void Rollback_AfterSingleSnapshot_RestoresSnapshotContent()
    {
        _temp.WriteFile("app.json", "v1");
        var key = GetPhysicalPath("/app.json");
        _sut.Snapshot(GetFileInfo("/app.json"));
        File.WriteAllText(key, "v2");

        Assert.True(_sut.Rollback(key));
        Assert.Equal("v1", File.ReadAllText(key));
    }

    /// <summary>
    /// 快照会累加，按步数回滚可以退回更早的版本
    /// </summary>
    [Fact]
    public void Rollback_WithSteps_WalksBackThroughAccumulatedVersions()
    {
        _temp.WriteFile("app.json", "v1");
        var key = GetPhysicalPath("/app.json");

        _sut.Snapshot(GetFileInfo("/app.json"));
        File.WriteAllText(key, "v2");
        _sut.Snapshot(GetFileInfo("/app.json"));
        File.WriteAllText(key, "v3");

        Assert.True(_sut.Rollback(key, steps: 2));
        Assert.Equal("v1", File.ReadAllText(key));
    }

    /// <summary>
    /// 只回退一步时落在最近一次快照上
    /// </summary>
    [Fact]
    public void Rollback_WithSingleStep_LandsOnMostRecentSnapshot()
    {
        _temp.WriteFile("app.json", "v1");
        var key = GetPhysicalPath("/app.json");

        _sut.Snapshot(GetFileInfo("/app.json"));
        File.WriteAllText(key, "v2");
        _sut.Snapshot(GetFileInfo("/app.json"));
        File.WriteAllText(key, "v3");

        Assert.True(_sut.Rollback(key));
        Assert.Equal("v2", File.ReadAllText(key));
    }

    /// <summary>
    /// 步数超过已有版本数时退到最早的版本
    /// </summary>
    [Fact]
    public void Rollback_WhenStepsExceedVersionCount_RestoresOldestVersion()
    {
        _temp.WriteFile("app.json", "v1");
        var key = GetPhysicalPath("/app.json");
        _sut.Snapshot(GetFileInfo("/app.json"));
        File.WriteAllText(key, "v2");

        Assert.True(_sut.Rollback(key, steps: 5));
        Assert.Equal("v1", File.ReadAllText(key));
    }

    /// <summary>
    /// 回滚会消费版本栈，栈空后再回滚返回 false 且不改动文件
    /// </summary>
    [Fact]
    public void Rollback_WhenVersionsExhausted_ReturnsFalseAndKeepsFile()
    {
        _temp.WriteFile("app.json", "v1");
        var key = GetPhysicalPath("/app.json");
        _sut.Snapshot(GetFileInfo("/app.json"));
        File.WriteAllText(key, "v2");

        Assert.True(_sut.Rollback(key));
        Assert.False(_sut.Rollback(key));
        Assert.Equal("v1", File.ReadAllText(key));
    }

    /// <summary>
    /// 步数非正数时直接返回 false，不消费版本栈
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rollback_WhenStepsNotPositive_ReturnsFalseWithoutConsumingVersions(int steps)
    {
        _temp.WriteFile("app.json", "v1");
        var key = GetPhysicalPath("/app.json");
        _sut.Snapshot(GetFileInfo("/app.json"));
        File.WriteAllText(key, "v2");

        Assert.False(_sut.Rollback(key, steps));
        Assert.Equal("v2", File.ReadAllText(key));

        // 版本栈没有被消费，后续正常回滚仍然可用
        Assert.True(_sut.Rollback(key));
        Assert.Equal("v1", File.ReadAllText(key));
    }

    /// <summary>
    /// 从未快照过的路径回滚返回 false
    /// </summary>
    [Fact]
    public void Rollback_ForUnknownPath_ReturnsFalse()
    {
        Assert.False(_sut.Rollback(Path.Combine(_temp.Root, "never-snapshotted.json")));
    }

    /// <summary>
    /// 目标文件已被删除时回滚返回 false，不会凭空重建文件
    /// </summary>
    [Fact]
    public void Rollback_WhenTargetFileDeleted_ReturnsFalse()
    {
        _temp.WriteFile("app.json", "v1");
        var key = GetPhysicalPath("/app.json");
        _sut.Snapshot(GetFileInfo("/app.json"));
        File.Delete(key);

        Assert.False(_sut.Rollback(key));
        Assert.False(File.Exists(key));
    }

    /// <summary>
    /// 回滚路径为 null 抛参数空异常
    /// </summary>
    [Fact]
    public void Rollback_WhenPathIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _ = _sut.Rollback(null!));
    }

    /// <summary>
    /// 回滚路径为空白抛参数异常
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rollback_WhenPathIsBlank_Throws(string path)
    {
        Assert.Throws<ArgumentException>(() => _ = _sut.Rollback(path));
    }

    /// <summary>
    /// 二进制内容按字节完整恢复
    /// </summary>
    [Fact]
    public void Rollback_ForBinaryContent_RestoresBytesExactly()
    {
        var path = Path.Combine(_temp.Root, "blob.bin");
        var original = new byte[] { 0x00, 0x01, 0xFF, 0x10, 0x7F };
        File.WriteAllBytes(path, original);
        var key = GetPhysicalPath("/blob.bin");

        _sut.Snapshot(GetFileInfo("/blob.bin"));
        File.WriteAllBytes(key, new byte[] { 0x42 });

        Assert.True(_sut.Rollback(key));
        Assert.Equal(original, File.ReadAllBytes(key));
    }

    /// <summary>
    /// 不同文件的版本栈互不干扰
    /// </summary>
    [Fact]
    public void Rollback_IsScopedPerFile()
    {
        _temp.WriteFile("a.json", "a1");
        _temp.WriteFile("b.json", "b1");
        var keyA = GetPhysicalPath("/a.json");
        var keyB = GetPhysicalPath("/b.json");
        _sut.Snapshot(GetFileInfo("/a.json"));
        _sut.Snapshot(GetFileInfo("/b.json"));
        File.WriteAllText(keyA, "a2");
        File.WriteAllText(keyB, "b2");

        Assert.True(_sut.Rollback(keyA));

        Assert.Equal("a1", File.ReadAllText(keyA));
        Assert.Equal("b2", File.ReadAllText(keyB));
    }

    /// <summary>
    /// 实现 IFileVersioningService 契约
    /// </summary>
    [Fact]
    public void Type_ImplementsServiceContract()
    {
        Assert.IsAssignableFrom<IFileVersioningService>(_sut);
    }

    private IFileInfo GetFileInfo(string subpath)
    {
        return ((IFileProvider)_provider).GetFileInfo(subpath);
    }

    private string GetPhysicalPath(string subpath)
    {
        var physicalPath = GetFileInfo(subpath).PhysicalPath;
        Assert.NotNull(physicalPath);
        return physicalPath;
    }
}
