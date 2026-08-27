// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Localization;
using System.Globalization;
using XiHan.Framework.Localization.Options;
using XiHan.Framework.Localization.Services;
using XiHan.Framework.Localization.Tests.TestSupport;

namespace XiHan.Framework.Localization.Tests.Services;

/// <summary>
/// JSON 字符串本地化器测试
/// </summary>
/// <remarks>
/// 核心契约是「JSON 优先、ResourceManager 兜底、都没有则返回键名且 ResourceNotFound=true」，
/// 这是 IStringLocalizer 的标准语义，上层视图与接口返回都依赖它来判断是否漏配文案。
/// 用例一律传入固定文化，避免依赖测试宿主的环境文化。
/// </remarks>
public class XiHanJsonStringLocalizerTests : IDisposable
{
    private static readonly CultureInfo Chinese = CultureInfo.GetCultureInfo("zh-CN");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

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
    /// JSON 命中时返回 JSON 文本且标记为已找到资源
    /// </summary>
    [Fact]
    public void Indexer_WhenJsonHasKey_ReturnsJsonValue()
    {
        AddHomeResources();
        var localizer = CreateLocalizer(new StubStringLocalizer(), Chinese);

        var result = localizer["Title"];

        Assert.Equal("Title", result.Name);
        Assert.Equal("首页", result.Value);
        Assert.False(result.ResourceNotFound);
    }

    /// <summary>
    /// JSON 未命中时改用兜底本地化器的文本
    /// </summary>
    [Fact]
    public void Indexer_WhenJsonMisses_UsesFallbackLocalizer()
    {
        AddHomeResources();
        var fallback = new StubStringLocalizer(new Dictionary<string, string> { ["OnlyFallback"] = "兜底文本" });
        var localizer = CreateLocalizer(fallback, Chinese);

        var result = localizer["OnlyFallback"];

        Assert.Equal("兜底文本", result.Value);
        Assert.False(result.ResourceNotFound);
    }

    /// <summary>
    /// JSON 与兜底都未命中时返回键名并标记资源未找到
    /// </summary>
    [Fact]
    public void Indexer_WhenJsonAndFallbackMiss_ReturnsKeyNameWithResourceNotFound()
    {
        AddHomeResources();
        var localizer = CreateLocalizer(new StubStringLocalizer(), Chinese);

        var result = localizer["MissingKey"];

        Assert.Equal("MissingKey", result.Name);
        Assert.Equal("MissingKey", result.Value);
        Assert.True(result.ResourceNotFound);
    }

    /// <summary>
    /// 键为空白时抛参数异常
    /// </summary>
    /// <param name="name">键</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Indexer_WhenNameBlank_ThrowsArgumentException(string name)
    {
        var localizer = CreateLocalizer(new StubStringLocalizer(), Chinese);

        Assert.Throws<ArgumentException>(() =>
        {
            _ = localizer[name];
        });
    }

    /// <summary>
    /// 键为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void Indexer_WhenNameNull_ThrowsArgumentNullException()
    {
        var localizer = CreateLocalizer(new StubStringLocalizer(), Chinese);

        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = localizer[null!];
        });
    }

    /// <summary>
    /// 带参数索引器会把占位符替换成实参
    /// </summary>
    [Fact]
    public void IndexerWithArguments_WhenJsonHasTemplate_FormatsPlaceholders()
    {
        AddHomeResources();
        var localizer = CreateLocalizer(new StubStringLocalizer(), Chinese);

        var result = localizer["Greeting", "曦寒"];

        Assert.Equal("你好，曦寒！", result.Value);
        Assert.False(result.ResourceNotFound);
    }

    /// <summary>
    /// 多个占位符按顺序替换
    /// </summary>
    [Fact]
    public void IndexerWithArguments_WithMultipleArguments_ReplacesInOrder()
    {
        AddHomeResources();
        var localizer = CreateLocalizer(new StubStringLocalizer(), Chinese);

        var result = localizer["Range", "1", "9"];

        Assert.Equal("从 1 到 9", result.Value);
    }

    /// <summary>
    /// 不传实参时原样返回模板，不做格式化
    /// </summary>
    [Fact]
    public void IndexerWithArguments_WithoutArguments_ReturnsRawTemplate()
    {
        AddHomeResources();
        var localizer = CreateLocalizer(new StubStringLocalizer(), Chinese);

        var result = localizer["Greeting", Array.Empty<object>()];

        Assert.Equal("你好，{0}！", result.Value);
        Assert.False(result.ResourceNotFound);
    }

    /// <summary>
    /// 带参数索引器在 JSON 未命中时同样走兜底本地化器
    /// </summary>
    [Fact]
    public void IndexerWithArguments_WhenJsonMisses_UsesFallbackLocalizer()
    {
        AddHomeResources();
        var fallback = new StubStringLocalizer(new Dictionary<string, string> { ["FallbackTemplate"] = "兜底 {0}" });
        var localizer = CreateLocalizer(fallback, Chinese);

        var result = localizer["FallbackTemplate", "X"];

        Assert.Equal("兜底 X", result.Value);
        Assert.False(result.ResourceNotFound);
    }

    /// <summary>
    /// 带参数索引器在全部未命中时返回键名并标记资源未找到
    /// </summary>
    [Fact]
    public void IndexerWithArguments_WhenAllMiss_ReturnsKeyNameWithResourceNotFound()
    {
        AddHomeResources();
        var localizer = CreateLocalizer(new StubStringLocalizer(), Chinese);

        var result = localizer["MissingTemplate", "X"];

        Assert.Equal("MissingTemplate", result.Value);
        Assert.True(result.ResourceNotFound);
    }

    /// <summary>
    /// 带参数索引器的键为空白时抛参数异常
    /// </summary>
    [Fact]
    public void IndexerWithArguments_WhenNameBlank_ThrowsArgumentException()
    {
        var localizer = CreateLocalizer(new StubStringLocalizer(), Chinese);

        Assert.Throws<ArgumentException>(() =>
        {
            _ = localizer["   ", "X"];
        });
    }

    /// <summary>
    /// 指定固定文化后忽略环境 UI 文化
    /// </summary>
    [Fact]
    public void Indexer_WhenFixedCultureGiven_IgnoresAmbientUiCulture()
    {
        AddHomeResources();
        var localizer = CreateLocalizer(new StubStringLocalizer(), English);

        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = Chinese;

            Assert.Equal("Home", localizer["Title"].Value);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    /// <summary>
    /// 未指定固定文化时跟随环境 UI 文化
    /// </summary>
    [Fact]
    public void Indexer_WithoutFixedCulture_FollowsAmbientUiCulture()
    {
        AddHomeResources();
        var localizer = CreateLocalizer(new StubStringLocalizer(), fixedCulture: null);

        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = English;
            Assert.Equal("Home", localizer["Title"].Value);

            CultureInfo.CurrentUICulture = Chinese;
            Assert.Equal("首页", localizer["Title"].Value);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    /// <summary>
    /// WithCulture 返回绑定到指定文化的新本地化器
    /// </summary>
    [Fact]
    public void WithCulture_ReturnsLocalizerBoundToRequestedCulture()
    {
        AddHomeResources();
        var localizer = CreateLocalizer(new StubStringLocalizer(), Chinese);

        var englishLocalizer = localizer.WithCulture(English);

        Assert.IsType<XiHanJsonStringLocalizer>(englishLocalizer);
        Assert.NotSame(localizer, englishLocalizer);
        Assert.Equal("Home", englishLocalizer["Title"].Value);
    }

    /// <summary>
    /// WithCulture 不会改变原本地化器绑定的文化
    /// </summary>
    [Fact]
    public void WithCulture_DoesNotMutateOriginalLocalizer()
    {
        AddHomeResources();
        var localizer = CreateLocalizer(new StubStringLocalizer(), Chinese);

        _ = localizer.WithCulture(English);

        Assert.Equal("首页", localizer["Title"].Value);
    }

    /// <summary>
    /// 全量取值时 JSON 条目覆盖兜底同名条目
    /// </summary>
    [Fact]
    public void GetAllStrings_JsonEntriesWinOverFallbackEntries()
    {
        AddHomeResources();
        var fallback = new StubStringLocalizer(new Dictionary<string, string>
        {
            ["Title"] = "兜底标题",
            ["OnlyFallback"] = "兜底独有"
        });
        var localizer = CreateLocalizer(fallback, Chinese);

        var all = localizer.GetAllStrings(includeParentCultures: true).ToList();

        Assert.Equal("首页", all.Single(x => x.Name == "Title").Value);
        Assert.Equal("兜底独有", all.Single(x => x.Name == "OnlyFallback").Value);
    }

    /// <summary>
    /// 全量取值按不区分大小写去重，同一键只出现一次
    /// </summary>
    [Fact]
    public void GetAllStrings_DeduplicatesKeysIgnoringCase()
    {
        AddHomeResources();
        var fallback = new StubStringLocalizer(new Dictionary<string, string> { ["title"] = "兜底标题" });
        var localizer = CreateLocalizer(fallback, Chinese);

        var all = localizer.GetAllStrings(includeParentCultures: true).ToList();

        Assert.Single(all, item => string.Equals(item.Name, "Title", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 全量取值包含 JSON 中的全部键
    /// </summary>
    [Fact]
    public void GetAllStrings_ContainsEveryJsonKey()
    {
        AddHomeResources();
        var localizer = CreateLocalizer(new StubStringLocalizer(), Chinese);

        var names = localizer.GetAllStrings(includeParentCultures: true).Select(x => x.Name).ToList();

        Assert.Contains("Title", names);
        Assert.Contains("Greeting", names);
        Assert.Contains("Range", names);
    }

    private void AddHomeResources()
    {
        _fileSystem.AddFile(
            "/Localization/Home.zh-CN.json",
            """{"Title":"首页","Greeting":"你好，{0}！","Range":"从 {0} 到 {1}"}""");
        _fileSystem.AddFile("/Localization/Home.en-US.json", """{"Title":"Home"}""");
    }

    private XiHanJsonStringLocalizer CreateLocalizer(IStringLocalizer fallback, CultureInfo? fixedCulture)
    {
        var store = new JsonLocalizationResourceStore(_fileSystem, _optionsMonitor);
        _stores.Add(store);
        return new XiHanJsonStringLocalizer("Home", store, fallback, fixedCulture);
    }
}
