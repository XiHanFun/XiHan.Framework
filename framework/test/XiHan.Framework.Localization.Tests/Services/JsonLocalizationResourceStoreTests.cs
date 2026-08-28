// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using XiHan.Framework.Localization.Options;
using XiHan.Framework.Localization.Services;
using XiHan.Framework.Localization.Tests.TestSupport;
using XiHan.Framework.VirtualFileSystem.Events;

namespace XiHan.Framework.Localization.Tests.Services;

/// <summary>
/// JSON 本地化资源存储测试
/// </summary>
/// <remarks>
/// 覆盖三条核心契约：
/// 1）文件名/目录/JSON 元数据到「资源名 + 文化」的解析规则；
/// 2）查找时的两级回退——先在同一文化内回退到默认资源，再沿文化链回退到父文化与默认文化；
/// 3）缓存版本随文件变化与选项变化递增，且释放后不再响应文件事件。
/// 虚拟文件系统用内存替身，避免真实磁盘监听的防抖时序影响断言。
/// </remarks>
public class JsonLocalizationResourceStoreTests : IDisposable
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
    /// 虚拟文件系统为空时构造函数抛参数空异常
    /// </summary>
    [Fact]
    public void Constructor_WhenVirtualFileSystemNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new JsonLocalizationResourceStore(null!, _optionsMonitor);
        });
    }

    /// <summary>
    /// 选项监控器为空时构造函数抛参数空异常
    /// </summary>
    [Fact]
    public void Constructor_WhenOptionsMonitorNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new JsonLocalizationResourceStore(_fileSystem, null!);
        });
    }

    /// <summary>
    /// 启用动态重载时按资源目录登记 JSON 监控
    /// </summary>
    [Fact]
    public void Constructor_WhenDynamicReloadEnabled_RegistersJsonWatchFilter()
    {
        _ = CreateStore();

        Assert.Contains("/Localization/**/*.json", _fileSystem.WatchFilters);
    }

    /// <summary>
    /// 关闭动态重载时不登记任何监控
    /// </summary>
    [Fact]
    public void Constructor_WhenDynamicReloadDisabled_DoesNotRegisterWatch()
    {
        _optionsMonitor.CurrentValue.EnableDynamicJsonReload = false;

        _ = CreateStore();

        Assert.Empty(_fileSystem.WatchFilters);
    }

    /// <summary>
    /// 请求文化下存在该键时直接返回对应文本
    /// </summary>
    [Fact]
    public void TryGetString_WhenKeyExistsInRequestedCulture_ReturnsValue()
    {
        _fileSystem.AddFile("/Localization/Default.zh-CN.json", """{"Title":"首页"}""");
        var store = CreateStore();

        var found = store.TryGetString("Default", Chinese, "Title", out var value);

        Assert.True(found);
        Assert.Equal("首页", value);
    }

    /// <summary>
    /// 所有回退位置都没有该键时返回 false 且值为空串
    /// </summary>
    [Fact]
    public void TryGetString_WhenKeyMissingEverywhere_ReturnsFalseAndEmptyValue()
    {
        _fileSystem.AddFile("/Localization/Default.zh-CN.json", """{"Title":"首页"}""");
        var store = CreateStore();

        var found = store.TryGetString("Default", Chinese, "NotExists", out var value);

        Assert.False(found);
        Assert.Equal(string.Empty, value);
    }

    /// <summary>
    /// 子文化缺失时沿父文化回退
    /// </summary>
    [Fact]
    public void TryGetString_WhenOnlyParentCultureHasKey_FallsBackToParentCulture()
    {
        _fileSystem.AddFile("/Localization/Default.zh.json", """{"Title":"中文通用首页"}""");
        var store = CreateStore();

        var found = store.TryGetString("Default", Chinese, "Title", out var value);

        Assert.True(found);
        Assert.Equal("中文通用首页", value);
    }

    /// <summary>
    /// 关闭父文化回退后不会命中父文化资源
    /// </summary>
    [Fact]
    public void TryGetString_WhenParentFallbackDisabled_DoesNotUseParentCulture()
    {
        _optionsMonitor.CurrentValue.FallbackToParentCultures = false;
        _optionsMonitor.CurrentValue.FallbackToDefaultCulture = false;
        _fileSystem.AddFile("/Localization/Default.zh.json", """{"Title":"中文通用首页"}""");
        var store = CreateStore();

        var found = store.TryGetString("Default", Chinese, "Title", out var value);

        Assert.False(found);
        Assert.Equal(string.Empty, value);
    }

    /// <summary>
    /// 请求文化及其父文化都缺失时回退到默认文化
    /// </summary>
    [Fact]
    public void TryGetString_WhenRequestedCultureMissing_FallsBackToDefaultCulture()
    {
        _fileSystem.AddFile("/Localization/Default.zh-CN.json", """{"Title":"首页"}""");
        var store = CreateStore();

        var found = store.TryGetString("Default", English, "Title", out var value);

        Assert.True(found);
        Assert.Equal("首页", value);
    }

    /// <summary>
    /// 关闭默认文化回退后跨文化查找失败
    /// </summary>
    [Fact]
    public void TryGetString_WhenDefaultCultureFallbackDisabled_ReturnsFalse()
    {
        _optionsMonitor.CurrentValue.FallbackToDefaultCulture = false;
        _fileSystem.AddFile("/Localization/Default.zh-CN.json", """{"Title":"首页"}""");
        var store = CreateStore();

        var found = store.TryGetString("Default", English, "Title", out _);

        Assert.False(found);
    }

    /// <summary>
    /// 同一文化内先回退默认资源，再考虑父文化与默认文化
    /// </summary>
    /// <remarks>
    /// Orders 资源只有 en-US 文本、Default 资源有 zh-CN 文本；请求 zh-CN 时应命中 Default 的 zh-CN，
    /// 而不是 Orders 的 en-US——资源回退优先级高于文化回退。
    /// </remarks>
    [Fact]
    public void TryGetString_WhenResourceMissesKey_FallsBackToDefaultResourceBeforeCultureChain()
    {
        _optionsMonitor.CurrentValue.DefaultCulture = "en-US";
        _fileSystem.AddFile("/Localization/Orders.en-US.json", """{"Ready":"orders-en"}""");
        _fileSystem.AddFile("/Localization/Default.zh-CN.json", """{"Ready":"default-zh"}""");
        var store = CreateStore();

        var found = store.TryGetString("Orders", Chinese, "Ready", out var value);

        Assert.True(found);
        Assert.Equal("default-zh", value);
    }

    /// <summary>
    /// 资源名为空白时按默认资源名查找
    /// </summary>
    /// <param name="resourceName">资源名</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryGetString_WhenResourceNameBlank_UsesDefaultResourceName(string? resourceName)
    {
        _fileSystem.AddFile("/Localization/Default.zh-CN.json", """{"Title":"首页"}""");
        var store = CreateStore();

        var found = store.TryGetString(resourceName!, Chinese, "Title", out var value);

        Assert.True(found);
        Assert.Equal("首页", value);
    }

    /// <summary>
    /// 文化参数为空时抛参数空异常
    /// </summary>
    [Fact]
    public void TryGetString_WhenCultureNull_ThrowsArgumentNullException()
    {
        var store = CreateStore();

        Assert.Throws<ArgumentNullException>(() =>
        {
            store.TryGetString("Default", null!, "Title", out _);
        });
    }

    /// <summary>
    /// 键为空白时抛参数异常
    /// </summary>
    /// <param name="name">键</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryGetString_WhenNameBlank_ThrowsArgumentException(string name)
    {
        var store = CreateStore();

        Assert.Throws<ArgumentException>(() =>
        {
            store.TryGetString("Default", Chinese, name, out _);
        });
    }

    /// <summary>
    /// 键为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void TryGetString_WhenNameNull_ThrowsArgumentNullException()
    {
        var store = CreateStore();

        Assert.Throws<ArgumentNullException>(() =>
        {
            store.TryGetString("Default", Chinese, null!, out _);
        });
    }

    /// <summary>
    /// 嵌套 JSON 对象被展平为点分键
    /// </summary>
    [Fact]
    public void TryGetString_WhenJsonIsNested_FlattensToDotSeparatedKey()
    {
        _fileSystem.AddFile("/Localization/Default.zh-CN.json", """{"Menu":{"Home":"首页","Sub":{"Deep":"深层"}}}""");
        var store = CreateStore();

        Assert.True(store.TryGetString("Default", Chinese, "Menu.Home", out var home));
        Assert.Equal("首页", home);
        Assert.True(store.TryGetString("Default", Chinese, "Menu.Sub.Deep", out var deep));
        Assert.Equal("深层", deep);
    }

    /// <summary>
    /// 键查找不区分大小写
    /// </summary>
    [Fact]
    public void TryGetString_KeyLookup_IsCaseInsensitive()
    {
        _fileSystem.AddFile("/Localization/Default.zh-CN.json", """{"Menu":{"Home":"首页"}}""");
        var store = CreateStore();

        var found = store.TryGetString("Default", Chinese, "menu.HOME", out var value);

        Assert.True(found);
        Assert.Equal("首页", value);
    }

    /// <summary>
    /// 资源名查找不区分大小写
    /// </summary>
    [Fact]
    public void TryGetString_ResourceLookup_IsCaseInsensitive()
    {
        _fileSystem.AddFile("/Localization/Orders.zh-CN.json", """{"Ready":"待发货"}""");
        var store = CreateStore();

        var found = store.TryGetString("ORDERS", Chinese, "Ready", out var value);

        Assert.True(found);
        Assert.Equal("待发货", value);
    }

    /// <summary>
    /// 非字符串 JSON 值被转换为文本
    /// </summary>
    [Fact]
    public void TryGetString_WhenJsonValueIsNotString_ConvertsToText()
    {
        _fileSystem.AddFile("/Localization/Default.zh-CN.json", """
            {
                "Count": 42,
                "Enabled": true,
                "Disabled": false,
                "Empty": null,
                "Items": ["a","b"]
            }
            """);
        var store = CreateStore();

        Assert.True(store.TryGetString("Default", Chinese, "Count", out var count));
        Assert.Equal("42", count);
        Assert.True(store.TryGetString("Default", Chinese, "Enabled", out var enabled));
        Assert.Equal(bool.TrueString, enabled);
        Assert.True(store.TryGetString("Default", Chinese, "Disabled", out var disabled));
        Assert.Equal(bool.FalseString, disabled);
        Assert.True(store.TryGetString("Default", Chinese, "Empty", out var empty));
        Assert.Equal(string.Empty, empty);
        Assert.True(store.TryGetString("Default", Chinese, "Items", out var items));
        Assert.Equal("""["a","b"]""", items);
    }

    /// <summary>
    /// JSON 内声明的资源名与文化优先于文件名推断
    /// </summary>
    [Fact]
    public void TryGetString_WhenJsonDeclaresResourceAndCulture_UsesJsonMetadata()
    {
        _fileSystem.AddFile(
            "/Localization/whatever.json",
            """{"resource":"Errors","culture":"en-US","texts":{"E001":"Boom"}}""");
        var store = CreateStore();

        var found = store.TryGetString("Errors", English, "E001", out var value);

        Assert.True(found);
        Assert.Equal("Boom", value);
    }

    /// <summary>
    /// 用 resources 包裹文本时同样被识别
    /// </summary>
    [Fact]
    public void TryGetString_WhenJsonUsesResourcesWrapper_ParsesTexts()
    {
        _fileSystem.AddFile(
            "/Localization/Default.zh-CN.json",
            """{"resources":{"Title":"首页"}}""");
        var store = CreateStore();

        var found = store.TryGetString("Default", Chinese, "Title", out var value);

        Assert.True(found);
        Assert.Equal("首页", value);
    }

    /// <summary>
    /// 文件名只有文化时用所在目录名作为资源名
    /// </summary>
    [Fact]
    public void TryGetString_WhenFileNamedByCultureOnly_UsesDirectoryAsResourceName()
    {
        _fileSystem.AddFile("/Localization/Orders/zh-CN.json", """{"Ready":"待发货"}""");
        var store = CreateStore();

        var found = store.TryGetString("Orders", Chinese, "Ready", out var value);

        Assert.True(found);
        Assert.Equal("待发货", value);
    }

    /// <summary>
    /// 空文本文件被跳过，不影响其他文件的加载
    /// </summary>
    [Fact]
    public void TryGetString_WhenFileHasNoTexts_IsIgnoredWithoutBreakingOthers()
    {
        _fileSystem.AddFile("/Localization/Empty.zh-CN.json", """{"texts":{}}""");
        _fileSystem.AddFile("/Localization/Default.zh-CN.json", """{"Title":"首页"}""");
        var store = CreateStore();

        Assert.False(store.TryGetString("Empty", Chinese, "Anything", out _));
        Assert.True(store.TryGetString("Default", Chinese, "Title", out var value));
        Assert.Equal("首页", value);
    }

    /// <summary>
    /// 资源目录之外的 JSON 文件不会被加载
    /// </summary>
    [Fact]
    public void TryGetString_WhenFileOutsideResourcesPath_IsNotLoaded()
    {
        _fileSystem.AddFile("/Other/Default.zh-CN.json", """{"Title":"不该被加载"}""");
        var store = CreateStore();

        Assert.False(store.TryGetString("Default", Chinese, "Title", out _));
    }

    /// <summary>
    /// 包含父文化时子文化文本覆盖父文化同名键
    /// </summary>
    [Fact]
    public void GetAllStrings_IncludingParentCultures_LetsChildCultureOverrideParent()
    {
        _fileSystem.AddFile("/Localization/Default.zh.json", """{"Shared":"parent","OnlyParent":"p"}""");
        _fileSystem.AddFile("/Localization/Default.zh-CN.json", """{"Shared":"child"}""");
        var store = CreateStore();

        var all = store.GetAllStrings("Default", Chinese, includeParentCultures: true);

        Assert.Equal("child", all.Single(x => x.Name == "Shared").Value);
        Assert.Equal("p", all.Single(x => x.Name == "OnlyParent").Value);
    }

    /// <summary>
    /// 不包含父文化时只返回精确文化的文本
    /// </summary>
    [Fact]
    public void GetAllStrings_ExcludingParentCultures_OnlyReturnsExactCulture()
    {
        _fileSystem.AddFile("/Localization/Default.zh.json", """{"Shared":"parent","OnlyParent":"p"}""");
        _fileSystem.AddFile("/Localization/Default.zh-CN.json", """{"Shared":"child"}""");
        var store = CreateStore();

        var all = store.GetAllStrings("Default", Chinese, includeParentCultures: false);

        var single = Assert.Single(all);
        Assert.Equal("Shared", single.Name);
        Assert.Equal("child", single.Value);
    }

    /// <summary>
    /// 非默认资源会合并默认资源的同文化文本，且自身文本优先
    /// </summary>
    [Fact]
    public void GetAllStrings_MergesDefaultResourceButKeepsRequestedResourceValue()
    {
        _fileSystem.AddFile("/Localization/Default.zh-CN.json", """{"Shared":"default","OnlyDefault":"d"}""");
        _fileSystem.AddFile("/Localization/Orders.zh-CN.json", """{"Shared":"orders"}""");
        var store = CreateStore();

        var all = store.GetAllStrings("Orders", Chinese, includeParentCultures: true);

        Assert.Equal("orders", all.Single(x => x.Name == "Shared").Value);
        Assert.Equal("d", all.Single(x => x.Name == "OnlyDefault").Value);
    }

    /// <summary>
    /// 返回的条目一律标记为已找到资源
    /// </summary>
    [Fact]
    public void GetAllStrings_AlwaysReportsResourceFound()
    {
        _fileSystem.AddFile("/Localization/Default.zh-CN.json", """{"Title":"首页","Sub":"子项"}""");
        var store = CreateStore();

        var all = store.GetAllStrings("Default", Chinese, includeParentCultures: true);

        Assert.NotEmpty(all);
        Assert.All(all, item => Assert.False(item.ResourceNotFound));
    }

    /// <summary>
    /// 文化参数为空时抛参数空异常
    /// </summary>
    [Fact]
    public void GetAllStrings_WhenCultureNull_ThrowsArgumentNullException()
    {
        var store = CreateStore();

        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = store.GetAllStrings("Default", null!, includeParentCultures: true);
        });
    }

    /// <summary>
    /// 初始版本号为零
    /// </summary>
    [Fact]
    public void Version_Initially_IsZero()
    {
        var store = CreateStore();

        Assert.Equal(0L, store.Version);
    }

    /// <summary>
    /// 资源目录下的 JSON 变化会重置缓存并递增版本号
    /// </summary>
    [Fact]
    public void Version_WhenJsonUnderResourcesPathChanges_IncrementsAndReloads()
    {
        var store = CreateStore();
        Assert.False(store.TryGetString("Default", Chinese, "Late", out _));

        _fileSystem.AddFile("/Localization/Default.zh-CN.json", """{"Late":"迟到的文本"}""");
        _fileSystem.RaiseFileChanged("/Localization/Default.zh-CN.json", FileChangeType.Created);

        Assert.Equal(1L, store.Version);
        Assert.True(store.TryGetString("Default", Chinese, "Late", out var value));
        Assert.Equal("迟到的文本", value);
    }

    /// <summary>
    /// 资源目录之外的文件变化不会重置缓存
    /// </summary>
    [Fact]
    public void Version_WhenChangedFileOutsideResourcesPath_DoesNotChange()
    {
        var store = CreateStore();

        _fileSystem.RaiseFileChanged("/Other/Default.zh-CN.json", FileChangeType.Modified);

        Assert.Equal(0L, store.Version);
    }

    /// <summary>
    /// 非 JSON 文件的变化不会重置缓存
    /// </summary>
    [Fact]
    public void Version_WhenChangedFileIsNotJson_DoesNotChange()
    {
        var store = CreateStore();

        _fileSystem.RaiseFileChanged("/Localization/readme.txt", FileChangeType.Modified);

        Assert.Equal(0L, store.Version);
    }

    /// <summary>
    /// 关闭动态重载后文件变化被忽略
    /// </summary>
    [Fact]
    public void Version_WhenDynamicReloadDisabled_DoesNotChange()
    {
        _optionsMonitor.CurrentValue.EnableDynamicJsonReload = false;
        var store = CreateStore();

        _fileSystem.RaiseFileChanged("/Localization/Default.zh-CN.json", FileChangeType.Modified);

        Assert.Equal(0L, store.Version);
    }

    /// <summary>
    /// 选项变更会重置缓存并按新的资源目录重新加载
    /// </summary>
    [Fact]
    public void Version_WhenOptionsChanged_IncrementsAndUsesNewResourcesPath()
    {
        _fileSystem.AddFile("/i18n/Default.zh-CN.json", """{"Title":"新目录首页"}""");
        var store = CreateStore();
        Assert.False(store.TryGetString("Default", Chinese, "Title", out _));

        _optionsMonitor.Set(new XiHanLocalizationOptions { ResourcesPath = "/i18n" });

        Assert.Equal(1L, store.Version);
        Assert.True(store.TryGetString("Default", Chinese, "Title", out var value));
        Assert.Equal("新目录首页", value);
    }

    /// <summary>
    /// 选项变更后按新资源目录重新登记监控
    /// </summary>
    [Fact]
    public void Version_WhenOptionsChanged_RegistersWatchForNewResourcesPath()
    {
        _ = CreateStore();

        _optionsMonitor.Set(new XiHanLocalizationOptions { ResourcesPath = "/i18n" });

        Assert.Contains("/i18n/**/*.json", _fileSystem.WatchFilters);
    }

    /// <summary>
    /// 释放后解绑文件变化事件，不再响应后续变化
    /// </summary>
    [Fact]
    public void Dispose_UnsubscribesFileChangedHandler()
    {
        var store = CreateStore();
        Assert.Equal(1, _fileSystem.FileChangedSubscriberCount);

        store.Dispose();

        Assert.Equal(0, _fileSystem.FileChangedSubscriberCount);
        _fileSystem.RaiseFileChanged("/Localization/Default.zh-CN.json", FileChangeType.Modified);
        Assert.Equal(0L, store.Version);
    }

    /// <summary>
    /// 重复释放是幂等的
    /// </summary>
    [Fact]
    public void Dispose_CalledTwice_IsIdempotent()
    {
        var store = CreateStore();

        store.Dispose();
        store.Dispose();

        Assert.Equal(0, _fileSystem.FileChangedSubscriberCount);
    }

    private JsonLocalizationResourceStore CreateStore()
    {
        var store = new JsonLocalizationResourceStore(_fileSystem, _optionsMonitor);
        _stores.Add(store);
        return store;
    }
}
