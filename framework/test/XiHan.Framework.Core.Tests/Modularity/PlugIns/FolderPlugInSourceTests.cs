// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Modularity.PlugIns;

namespace XiHan.Framework.Core.Tests.Modularity.PlugIns;

/// <summary>
/// 文件夹插件源测试
/// </summary>
/// <remarks>
/// 文件夹插件源按扩展名筛出程序集文件后逐个加载并扫描模块类型。
/// 用例只在临时目录里放非程序集文件，验证「筛选发生在加载之前」——
/// 真实加载外部程序集会污染默认程序集加载上下文，不在单元测试里做。
/// </remarks>
public class FolderPlugInSourceTests : IDisposable
{
    private readonly string _folder;

    /// <summary>
    /// 构造函数，准备独立的临时目录
    /// </summary>
    public FolderPlugInSourceTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "XiHanTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    /// <summary>
    /// 构造后保留文件夹与搜索选项，且默认不带过滤器
    /// </summary>
    [Fact]
    public void Constructor_KeepsFolderAndSearchOption()
    {
        var source = new FolderPlugInSource(_folder, SearchOption.AllDirectories);

        Assert.Equal(_folder, source.Folder);
        Assert.Equal(SearchOption.AllDirectories, source.SearchOption);
        Assert.Null(source.Filter);
    }

    /// <summary>
    /// 默认只搜索顶层目录
    /// </summary>
    [Fact]
    public void Constructor_DefaultsToTopDirectoryOnly()
    {
        var source = new FolderPlugInSource(_folder);

        Assert.Equal(SearchOption.TopDirectoryOnly, source.SearchOption);
    }

    /// <summary>
    /// 文件夹为空引用时抛出
    /// </summary>
    [Fact]
    public void Constructor_WhenFolderNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new FolderPlugInSource(null!);
        });
    }

    /// <summary>
    /// 空目录中扫描不到任何模块
    /// </summary>
    [Fact]
    public void GetModules_WhenFolderEmpty_ReturnsEmpty()
    {
        var source = new FolderPlugInSource(_folder);

        Assert.Empty(source.GetModules());
    }

    /// <summary>
    /// 非程序集文件在加载前就被扩展名筛掉
    /// </summary>
    [Fact]
    public void GetModules_WhenOnlyNonAssemblyFiles_ReturnsEmpty()
    {
        File.WriteAllText(Path.Combine(_folder, "readme.txt"), "not an assembly");
        File.WriteAllText(Path.Combine(_folder, "config.json"), "{}");
        var source = new FolderPlugInSource(_folder);

        Assert.Empty(source.GetModules());
    }

    /// <summary>
    /// 过滤器可被设置并读回
    /// </summary>
    [Fact]
    public void Filter_IsAssignable()
    {
        var source = new FolderPlugInSource(_folder)
        {
            Filter = path => path.EndsWith("plugin.dll", StringComparison.OrdinalIgnoreCase)
        };

        Assert.NotNull(source.Filter);
        Assert.True(source.Filter("some/plugin.dll"));
        Assert.False(source.Filter("some/other.dll"));
    }

    /// <summary>
    /// 过滤器生效时被排除的文件不参与加载
    /// </summary>
    [Fact]
    public void GetModules_WhenFilterExcludesEverything_ReturnsEmpty()
    {
        File.WriteAllText(Path.Combine(_folder, "fake.dll"), "not a real assembly");
        var source = new FolderPlugInSource(_folder)
        {
            Filter = _ => false
        };

        Assert.Empty(source.GetModules());
    }

    /// <summary>
    /// 目录不存在时抛出目录未找到
    /// </summary>
    [Fact]
    public void GetModules_WhenFolderMissing_Throws()
    {
        var source = new FolderPlugInSource(Path.Combine(_folder, "absent"));

        Assert.Throws<DirectoryNotFoundException>(() => source.GetModules());
    }

    /// <summary>
    /// 清理临时目录
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, true);
            }
        }
        catch (IOException)
        {
            // 临时目录清理失败不影响断言结论
        }
        catch (UnauthorizedAccessException)
        {
            // 临时目录清理失败不影响断言结论
        }

        GC.SuppressFinalize(this);
    }
}
