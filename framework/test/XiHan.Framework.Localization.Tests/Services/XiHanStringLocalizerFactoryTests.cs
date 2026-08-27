// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using XiHan.Framework.Localization.Options;
using XiHan.Framework.Localization.Services;
using XiHan.Framework.Localization.Tests.TestSupport;

namespace XiHan.Framework.Localization.Tests.Services;

/// <summary>
/// XiHan 本地化工厂测试
/// </summary>
/// <remarks>
/// 工厂有两条契约值得锁死：
/// 1）资源名推断——按类型创建取类型短名，按基础名创建取基础名最后一段（兼容 . / \ 分隔）；
/// 2）ResourceManager 兜底不可用（location 不是可加载程序集）时必须降级为「仅 JSON」而不是整体抛异常，
///    这是 JSON-first 资源（无 backing 程序集）能正常工作的前提。
/// 用例中的 JSON 一律放在默认文化 zh-CN 下，借助默认文化回退避免依赖测试宿主的环境文化。
/// </remarks>
public class XiHanStringLocalizerFactoryTests : IDisposable
{
    private const string UnloadableAssemblyName = "XiHanTests.NotExistingAssembly.ForLocalization";

    private readonly FakeVirtualFileSystem _fileSystem = new();
    private readonly TestOptionsMonitor<XiHanLocalizationOptions> _optionsMonitor = new(new XiHanLocalizationOptions());
    private readonly List<JsonLocalizationResourceStore> _stores = [];

    /// <summary>
    /// 释放本用例创建的全部资源存储
    /// </summary>
    public void Dispose()
    {
        foreach (var store in _stores)
        {
            store.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 按类型创建时以类型短名作为 JSON 资源名
    /// </summary>
    [Fact]
    public void Create_ByType_UsesTypeShortNameAsJsonResourceName()
    {
        _fileSystem.AddFile("/Localization/LocalizationDiProbe.zh-CN.json", """{"Hello":"你好"}""");
        var factory = CreateFactory();

        var localizer = factory.Create(typeof(LocalizationDiProbe));

        Assert.IsType<XiHanJsonStringLocalizer>(localizer);
        Assert.Equal("你好", localizer["Hello"].Value);
    }

    /// <summary>
    /// 按类型创建的本地化器会被缓存复用
    /// </summary>
    [Fact]
    public void Create_ByType_ReturnsCachedInstanceForSameType()
    {
        var factory = CreateFactory();

        var first = factory.Create(typeof(LocalizationDiProbe));
        var second = factory.Create(typeof(LocalizationDiProbe));

        Assert.Same(first, second);
    }

    /// <summary>
    /// 不同类型得到不同的本地化器
    /// </summary>
    [Fact]
    public void Create_ByType_ReturnsDifferentInstancesForDifferentTypes()
    {
        var factory = CreateFactory();

        var first = factory.Create(typeof(LocalizationDiProbe));
        var second = factory.Create(typeof(LocalizationDiOtherProbe));

        Assert.NotSame(first, second);
    }

    /// <summary>
    /// 类型为空时抛参数空异常
    /// </summary>
    [Fact]
    public void Create_ByType_WhenTypeNull_ThrowsArgumentNullException()
    {
        var factory = CreateFactory();

        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = factory.Create((Type)null!);
        });
    }

    /// <summary>
    /// 按基础名创建时取最后一段作为 JSON 资源名
    /// </summary>
    [Fact]
    public void Create_ByBaseName_UsesLastSegmentAsJsonResourceName()
    {
        _fileSystem.AddFile("/Localization/Errors.zh-CN.json", """{"E001":"系统错误"}""");
        var factory = CreateFactory();

        var localizer = factory.Create("XiHan.Framework.Localization.Resources.Errors", UnloadableAssemblyName);

        Assert.Equal("系统错误", localizer["E001"].Value);
    }

    /// <summary>
    /// 基础名中的路径分隔符按点号同等处理
    /// </summary>
    /// <param name="baseName">基础名</param>
    [Theory]
    [InlineData("Resources/Sub/Errors")]
    [InlineData("Resources\\Sub\\Errors")]
    [InlineData("Errors")]
    public void Create_ByBaseName_NormalizesPathSeparators(string baseName)
    {
        _fileSystem.AddFile("/Localization/Errors.zh-CN.json", """{"E001":"系统错误"}""");
        var factory = CreateFactory();

        var localizer = factory.Create(baseName, UnloadableAssemblyName);

        Assert.Equal("系统错误", localizer["E001"].Value);
    }

    /// <summary>
    /// ResourceManager 兜底不可用时未命中的键按标准契约返回键名
    /// </summary>
    /// <remarks>
    /// location 不是可加载程序集，工厂内部会捕获加载异常并降级为空兜底；
    /// 这里验证降级后仍然是「返回键名 + ResourceNotFound=true」，而不是抛出程序集加载异常。
    /// </remarks>
    [Fact]
    public void Create_ByBaseName_WhenResourceManagerUnavailable_ReportsResourceNotFound()
    {
        var factory = CreateFactory();

        var localizer = factory.Create("Errors", UnloadableAssemblyName);
        var result = localizer["NotConfiguredKey"];

        Assert.Equal("NotConfiguredKey", result.Name);
        Assert.Equal("NotConfiguredKey", result.Value);
        Assert.True(result.ResourceNotFound);
    }

    /// <summary>
    /// ResourceManager 兜底不可用时全量取值返回 JSON 中的条目
    /// </summary>
    [Fact]
    public void Create_ByBaseName_WhenResourceManagerUnavailable_GetAllStringsReturnsJsonEntries()
    {
        _fileSystem.AddFile("/Localization/Errors.zh-CN.json", """{"E001":"系统错误","E002":"参数错误"}""");
        var factory = CreateFactory();

        var localizer = factory.Create("Errors", UnloadableAssemblyName);
        var names = localizer.GetAllStrings(includeParentCultures: true).Select(x => x.Name).ToList();

        Assert.Contains("E001", names);
        Assert.Contains("E002", names);
    }

    /// <summary>
    /// 相同基础名与位置的本地化器会被缓存复用
    /// </summary>
    [Fact]
    public void Create_ByBaseName_ReturnsCachedInstanceForSameKey()
    {
        var factory = CreateFactory();

        var first = factory.Create("Errors", UnloadableAssemblyName);
        var second = factory.Create("Errors", UnloadableAssemblyName);

        Assert.Same(first, second);
    }

    /// <summary>
    /// 位置不同时缓存键不同，返回不同实例
    /// </summary>
    [Fact]
    public void Create_ByBaseName_ReturnsDifferentInstancesForDifferentLocations()
    {
        var factory = CreateFactory();

        var first = factory.Create("Errors", UnloadableAssemblyName);
        var second = factory.Create("Errors", UnloadableAssemblyName + ".Other");

        Assert.NotSame(first, second);
    }

    /// <summary>
    /// 基础名为空白时抛参数异常
    /// </summary>
    /// <param name="baseName">基础名</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ByBaseName_WhenBaseNameBlank_ThrowsArgumentException(string baseName)
    {
        var factory = CreateFactory();

        Assert.Throws<ArgumentException>(() =>
        {
            _ = factory.Create(baseName, UnloadableAssemblyName);
        });
    }

    /// <summary>
    /// 位置为空白时抛参数异常
    /// </summary>
    /// <param name="location">位置</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ByBaseName_WhenLocationBlank_ThrowsArgumentException(string location)
    {
        var factory = CreateFactory();

        Assert.Throws<ArgumentException>(() =>
        {
            _ = factory.Create("Errors", location);
        });
    }

    /// <summary>
    /// 基础名为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void Create_ByBaseName_WhenBaseNameNull_ThrowsArgumentNullException()
    {
        var factory = CreateFactory();

        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = factory.Create(null!, UnloadableAssemblyName);
        });
    }

    private XiHanStringLocalizerFactory CreateFactory()
    {
        var store = new JsonLocalizationResourceStore(_fileSystem, _optionsMonitor);
        _stores.Add(store);

        var resourceManagerFactory = new ResourceManagerStringLocalizerFactory(
            new OptionsWrapper<LocalizationOptions>(new LocalizationOptions()),
            NullLoggerFactory.Instance);

        return new XiHanStringLocalizerFactory(store, resourceManagerFactory);
    }
}
