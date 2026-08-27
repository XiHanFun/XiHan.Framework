// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using XiHan.Framework.Upgrade.Options;
using XiHan.Framework.Upgrade.Services;

namespace XiHan.Framework.Upgrade.Tests;

/// <summary>
/// 文件系统升级脚本提供者测试
/// </summary>
/// <remarks>
/// 用真实临时目录造脚本文件，覆盖脚本发现规则（只认合法版本目录、只认顶层 *.sql）
/// 与排序规则（版本按语义序、同版本内按脚本名序）。排序错了会导致脚本乱序执行，
/// 是这个类最值钱的契约。
/// </remarks>
public class FileSystemUpgradeScriptProviderTests : IDisposable
{
    private readonly string _rootPath;
    private readonly string _relativeRootName;
    private readonly string _relativeRootPath;

    /// <summary>
    /// 构造函数，准备独立的临时脚本根目录
    /// </summary>
    public FileSystemUpgradeScriptProviderTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "XiHanTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _relativeRootName = "XiHanUpgradeScripts_" + Guid.NewGuid().ToString("N");
        _relativeRootPath = Path.Combine(AppContext.BaseDirectory, _relativeRootName);
    }

    /// <summary>
    /// 根目录不存在时返回空列表而不是抛异常
    /// </summary>
    [Fact]
    public async Task GetScriptsAsync_WhenRootDirectoryMissing_ReturnsEmpty()
    {
        var provider = CreateProvider(Path.Combine(_rootPath, "not-exists"));

        var scripts = await provider.GetScriptsAsync(TestContext.Current.CancellationToken);

        Assert.Empty(scripts);
    }

    /// <summary>
    /// 根目录存在但没有任何版本目录时返回空列表
    /// </summary>
    [Fact]
    public async Task GetScriptsAsync_WhenNoVersionDirectory_ReturnsEmpty()
    {
        var provider = CreateProvider(_rootPath);

        var scripts = await provider.GetScriptsAsync(TestContext.Current.CancellationToken);

        Assert.Empty(scripts);
    }

    /// <summary>
    /// 版本目录按语义序排列，1.0.10 排在 1.0.2 之后
    /// </summary>
    [Fact]
    public async Task GetScriptsAsync_OrdersVersionDirectoriesSemantically()
    {
        WriteScript("1.0.10", "a.sql");
        WriteScript("1.0.2", "a.sql");
        WriteScript("0.9.9", "a.sql");
        WriteScript("2.0.0", "a.sql");
        var provider = CreateProvider(_rootPath);

        var scripts = await provider.GetScriptsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["0.9.9", "1.0.2", "1.0.10", "2.0.0"], scripts.Select(script => script.Version));
    }

    /// <summary>
    /// 同一版本目录内按脚本名忽略大小写升序
    /// </summary>
    [Fact]
    public async Task GetScriptsAsync_WithinSameVersion_OrdersByScriptNameIgnoreCase()
    {
        WriteScript("1.0.0", "B_second.sql");
        WriteScript("1.0.0", "a_first.sql");
        WriteScript("1.0.0", "c_third.sql");
        var provider = CreateProvider(_rootPath);

        var scripts = await provider.GetScriptsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["a_first.sql", "B_second.sql", "c_third.sql"], scripts.Select(script => script.ScriptName));
    }

    /// <summary>
    /// 目录名不是合法版本号时整个目录被跳过
    /// </summary>
    [Fact]
    public async Task GetScriptsAsync_WhenDirectoryNameIsNotVersion_SkipsDirectory()
    {
        WriteScript("docs", "a.sql");
        WriteScript("v1.0.0", "a.sql");
        WriteScript("1.0.0", "a.sql");
        var provider = CreateProvider(_rootPath);

        var scripts = await provider.GetScriptsAsync(TestContext.Current.CancellationToken);

        var script = Assert.Single(scripts);
        Assert.Equal("1.0.0", script.Version);
    }

    /// <summary>
    /// 非 sql 文件不会被当成升级脚本
    /// </summary>
    [Fact]
    public async Task GetScriptsAsync_IgnoresNonSqlFiles()
    {
        WriteScript("1.0.0", "a.sql");
        WriteFile("1.0.0", "readme.txt", "not a script");
        WriteFile("1.0.0", "notes.md", "not a script");
        var provider = CreateProvider(_rootPath);

        var scripts = await provider.GetScriptsAsync(TestContext.Current.CancellationToken);

        var script = Assert.Single(scripts);
        Assert.Equal("a.sql", script.ScriptName);
    }

    /// <summary>
    /// 版本目录下的子目录不会被递归扫描
    /// </summary>
    /// <remarks>
    /// 平铺与嵌套的差异曾经导致脚本静默不执行，这里显式锁住「只扫顶层」的约定。
    /// </remarks>
    [Fact]
    public async Task GetScriptsAsync_DoesNotScanNestedDirectories()
    {
        WriteScript("1.0.0", "top.sql");
        var nested = Path.Combine(_rootPath, "1.0.0", "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "deep.sql"), "-- deep");
        var provider = CreateProvider(_rootPath);

        var scripts = await provider.GetScriptsAsync(TestContext.Current.CancellationToken);

        var script = Assert.Single(scripts);
        Assert.Equal("top.sql", script.ScriptName);
    }

    /// <summary>
    /// 空的版本目录不产出脚本
    /// </summary>
    [Fact]
    public async Task GetScriptsAsync_WhenVersionDirectoryEmpty_ProducesNoScript()
    {
        Directory.CreateDirectory(Path.Combine(_rootPath, "1.0.0"));
        WriteScript("1.1.0", "a.sql");
        var provider = CreateProvider(_rootPath);

        var scripts = await provider.GetScriptsAsync(TestContext.Current.CancellationToken);

        var script = Assert.Single(scripts);
        Assert.Equal("1.1.0", script.Version);
    }

    /// <summary>
    /// 脚本版本原样取自目录名，脚本路径指向真实存在的文件
    /// </summary>
    [Fact]
    public async Task GetScriptsAsync_ReportsRawDirectoryNameAndRealPath()
    {
        WriteScript("1.0", "a.sql", "-- content");
        var provider = CreateProvider(_rootPath);

        var scripts = await provider.GetScriptsAsync(TestContext.Current.CancellationToken);

        var script = Assert.Single(scripts);
        Assert.Equal("1.0", script.Version);
        Assert.Equal("a.sql", script.ScriptName);
        Assert.True(File.Exists(script.ScriptPath));
        Assert.Equal("-- content", await File.ReadAllTextAsync(script.ScriptPath, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 带预发布后缀的版本目录同样会被识别，版本值保持原样
    /// </summary>
    [Fact]
    public async Task GetScriptsAsync_WhenDirectoryHasPreReleaseSuffix_StillDiscovered()
    {
        WriteScript("1.2.0-beta", "a.sql");
        var provider = CreateProvider(_rootPath);

        var scripts = await provider.GetScriptsAsync(TestContext.Current.CancellationToken);

        var script = Assert.Single(scripts);
        Assert.Equal("1.2.0-beta", script.Version);
    }

    /// <summary>
    /// 相对路径基于应用基目录解析
    /// </summary>
    [Fact]
    public async Task GetScriptsAsync_WhenRootPathIsRelative_ResolvesAgainstBaseDirectory()
    {
        var versionDirectory = Path.Combine(_relativeRootPath, "1.0.0");
        Directory.CreateDirectory(versionDirectory);
        File.WriteAllText(Path.Combine(versionDirectory, "a.sql"), "-- relative");
        var provider = CreateProvider(_relativeRootName);

        var scripts = await provider.GetScriptsAsync(TestContext.Current.CancellationToken);

        var script = Assert.Single(scripts);
        Assert.Equal("1.0.0", script.Version);
        Assert.Equal("a.sql", script.ScriptName);
    }

    /// <summary>
    /// 跨版本与同版本的排序规则叠加后仍然稳定
    /// </summary>
    [Fact]
    public async Task GetScriptsAsync_OrdersByVersionThenScriptName()
    {
        WriteScript("1.1.0", "01_first.sql");
        WriteScript("1.0.0", "02_second.sql");
        WriteScript("1.0.0", "01_first.sql");
        var provider = CreateProvider(_rootPath);

        var scripts = await provider.GetScriptsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            ["1.0.0/01_first.sql", "1.0.0/02_second.sql", "1.1.0/01_first.sql"],
            scripts.Select(script => $"{script.Version}/{script.ScriptName}"));
    }

    /// <summary>
    /// 清理临时目录
    /// </summary>
    public void Dispose()
    {
        DeleteDirectory(_rootPath);
        DeleteDirectory(_relativeRootPath);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 删除目录，忽略文件占用等异常
    /// </summary>
    /// <param name="path">目录路径</param>
    private static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// 创建提供者
    /// </summary>
    /// <param name="rootPath">脚本根目录</param>
    /// <returns>提供者</returns>
    private static FileSystemUpgradeScriptProvider CreateProvider(string rootPath)
    {
        var options = new XiHanUpgradeOptions { MigrationsRootPath = rootPath };
        return new FileSystemUpgradeScriptProvider(new OptionsWrapper<XiHanUpgradeOptions>(options));
    }

    /// <summary>
    /// 写入一个升级脚本文件
    /// </summary>
    /// <param name="version">版本目录名</param>
    /// <param name="scriptName">脚本文件名</param>
    /// <param name="content">脚本内容</param>
    private void WriteScript(string version, string scriptName, string content = "-- noop")
    {
        WriteFile(version, scriptName, content);
    }

    /// <summary>
    /// 写入任意文件
    /// </summary>
    /// <param name="version">版本目录名</param>
    /// <param name="fileName">文件名</param>
    /// <param name="content">文件内容</param>
    private void WriteFile(string version, string fileName, string content)
    {
        var directory = Path.Combine(_rootPath, version);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, fileName), content);
    }
}
