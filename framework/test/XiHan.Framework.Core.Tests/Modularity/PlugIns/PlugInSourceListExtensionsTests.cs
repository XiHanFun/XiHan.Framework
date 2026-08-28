// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Modularity.PlugIns;

namespace XiHan.Framework.Core.Tests.Modularity.PlugIns;

/// <summary>
/// 插件源列表扩展测试
/// </summary>
/// <remarks>
/// 三个扩展方法各自往列表里追加一种插件源，是应用配置阶段声明插件的唯一入口；
/// 列表为空引用时必须立刻报参数错误，避免把问题拖到模块加载阶段才暴露成空引用。
/// </remarks>
public class PlugInSourceListExtensionsTests
{
    /// <summary>
    /// 新建的插件源列表为空
    /// </summary>
    [Fact]
    public void PlugInSourceList_StartsEmpty()
    {
        Assert.Empty(new PlugInSourceList());
    }

    /// <summary>
    /// 添加类型时追加类型插件源
    /// </summary>
    [Fact]
    public void AddTypes_AppendsTypePlugInSource()
    {
        var list = new PlugInSourceList();

        list.AddTypes(typeof(PlsSampleModule));

        var source = Assert.IsType<TypePlugInSource>(Assert.Single(list));
        Assert.Equal(typeof(PlsSampleModule), Assert.Single(source.GetModules()));
    }

    /// <summary>
    /// 添加文件夹时追加文件夹插件源并带上搜索选项
    /// </summary>
    [Fact]
    public void AddFolder_AppendsFolderPlugInSourceWithSearchOption()
    {
        var list = new PlugInSourceList();

        list.AddFolder("plugins", SearchOption.AllDirectories);

        var source = Assert.IsType<FolderPlugInSource>(Assert.Single(list));
        Assert.Equal("plugins", source.Folder);
        Assert.Equal(SearchOption.AllDirectories, source.SearchOption);
    }

    /// <summary>
    /// 添加文件夹时默认只搜索顶层目录
    /// </summary>
    [Fact]
    public void AddFolder_DefaultsToTopDirectoryOnly()
    {
        var list = new PlugInSourceList();

        list.AddFolder("plugins");

        var source = Assert.IsType<FolderPlugInSource>(Assert.Single(list));
        Assert.Equal(SearchOption.TopDirectoryOnly, source.SearchOption);
    }

    /// <summary>
    /// 添加文件时追加文件插件源并保留路径顺序
    /// </summary>
    [Fact]
    public void AddFiles_AppendsFilePlugInSourceWithPaths()
    {
        var list = new PlugInSourceList();

        list.AddFiles("first.dll", "second.dll");

        var source = Assert.IsType<FilePlugInSource>(Assert.Single(list));
        Assert.Equal(2, source.FilePaths.Length);
        Assert.Equal("first.dll", source.FilePaths[0]);
        Assert.Equal("second.dll", source.FilePaths[1]);
    }

    /// <summary>
    /// 多次添加按调用顺序累积
    /// </summary>
    [Fact]
    public void AddSources_AccumulatesInCallOrder()
    {
        var list = new PlugInSourceList();

        list.AddTypes(typeof(PlsSampleModule));
        list.AddFiles("first.dll");
        list.AddFolder("plugins");

        Assert.Equal(3, list.Count);
        Assert.IsType<TypePlugInSource>(list[0]);
        Assert.IsType<FilePlugInSource>(list[1]);
        Assert.IsType<FolderPlugInSource>(list[2]);
    }

    /// <summary>
    /// 列表为空引用时抛出
    /// </summary>
    [Fact]
    public void Extensions_WhenListNull_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => PlugInSourceListExtensions.AddTypes(null!, typeof(PlsSampleModule)));
        Assert.Throws<ArgumentNullException>(() => PlugInSourceListExtensions.AddFiles(null!, "first.dll"));
        Assert.Throws<ArgumentNullException>(() => PlugInSourceListExtensions.AddFolder(null!, "plugins"));
    }
}
