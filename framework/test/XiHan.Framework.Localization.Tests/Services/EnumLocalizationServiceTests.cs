// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using XiHan.Framework.Localization.Abstractions.Enums;
using XiHan.Framework.Localization.Options;
using XiHan.Framework.Localization.Services;
using XiHan.Framework.Localization.Tests.TestSupport;

namespace XiHan.Framework.Localization.Tests.Services;

/// <summary>
/// 枚举本地化服务测试
/// </summary>
/// <remarks>
/// 重点覆盖两件事：
/// 1）候选键的尝试顺序——特性键 &gt; 前缀键 &gt; 「类型.字段」&gt; 「类型_字段」&gt; 裸字段名，
///    任一命中即停；全部落空（含命中但值为空白）时降级为枚举自身的描述；
/// 2）类型名解析——短名与完整名都能定位到枚举类型，未知名称抛 KeyNotFoundException 而 TryGet 吞掉它。
/// 用例中的 JSON 一律放在默认文化 zh-CN 下，借助默认文化回退避免依赖测试宿主的环境文化。
/// </remarks>
public class EnumLocalizationServiceTests : IDisposable
{
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
    /// 枚举类型为空时抛参数空异常
    /// </summary>
    [Fact]
    public void Get_ByType_WhenTypeNull_ThrowsArgumentNullException()
    {
        var service = CreateService();

        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = service.Get((Type)null!);
        });
    }

    /// <summary>
    /// 传入非枚举类型时抛参数异常
    /// </summary>
    [Fact]
    public void Get_ByType_WhenTypeIsNotEnum_ThrowsArgumentException()
    {
        var service = CreateService();

        var exception = Assert.Throws<ArgumentException>(() =>
        {
            _ = service.Get(typeof(LocalizationDiProbe));
        });

        Assert.Contains("不是枚举类型", exception.Message);
    }

    /// <summary>
    /// 定义级元数据来自枚举类型本身
    /// </summary>
    [Fact]
    public void Get_ByType_FillsDefinitionMetadata()
    {
        var service = CreateService();

        var definition = service.Get(typeof(LocalizationTestOrderStatus), ChineseQuery());

        Assert.Equal(nameof(LocalizationTestOrderStatus), definition.EnumName);
        Assert.Equal(typeof(LocalizationTestOrderStatus).FullName, definition.FullName);
        Assert.Equal(nameof(LocalizationTestOrderStatus), definition.DisplayName);
        Assert.Equal("zh-CN", definition.CultureName);
        Assert.False(definition.IsFlags);
        Assert.Equal(typeof(int).FullName, definition.UnderlyingTypeName);
        Assert.Equal("Enums", definition.ResourceName);
    }

    /// <summary>
    /// 「类型.字段」形式的键命中时用作显示标签
    /// </summary>
    [Fact]
    public void Get_ByType_WhenDottedKeyExists_UsesLocalizedLabel()
    {
        AddEnumResources();
        var service = CreateService();

        var item = SingleItem(service.Get(typeof(LocalizationTestOrderStatus), ChineseQuery()), nameof(LocalizationTestOrderStatus.Pending));

        Assert.Equal("待处理", item.Label);
        Assert.Equal("LocalizationTestOrderStatus.Pending", item.LocalizationKey);
    }

    /// <summary>
    /// 「类型_字段」形式的键命中时用作显示标签
    /// </summary>
    [Fact]
    public void Get_ByType_WhenUnderscoreKeyExists_UsesLocalizedLabel()
    {
        AddEnumResources();
        var service = CreateService();

        var item = SingleItem(service.Get(typeof(LocalizationTestOrderStatus), ChineseQuery()), nameof(LocalizationTestOrderStatus.Paid));

        Assert.Equal("已支付", item.Label);
        Assert.Equal("LocalizationTestOrderStatus_Paid", item.LocalizationKey);
    }

    /// <summary>
    /// 裸字段名形式的键命中时用作显示标签
    /// </summary>
    [Fact]
    public void Get_ByType_WhenBareKeyExists_UsesLocalizedLabel()
    {
        AddEnumResources();
        var service = CreateService();

        var item = SingleItem(service.Get(typeof(LocalizationTestOrderStatus), ChineseQuery()), nameof(LocalizationTestOrderStatus.Shipped));

        Assert.Equal("已发货", item.Label);
        Assert.Equal(nameof(LocalizationTestOrderStatus.Shipped), item.LocalizationKey);
    }

    /// <summary>
    /// 命中的本地化文本为空白时降级为枚举描述
    /// </summary>
    [Fact]
    public void Get_ByType_WhenLocalizedTextIsBlank_FallsBackToDescription()
    {
        AddEnumResources();
        var service = CreateService();

        var item = SingleItem(service.Get(typeof(LocalizationTestOrderStatus), ChineseQuery()), nameof(LocalizationTestOrderStatus.Cancelled));

        Assert.Equal("已取消原始描述", item.Label);
        Assert.Equal("已取消原始描述", item.Description);
    }

    /// <summary>
    /// 完全没有本地化资源时全部降级为枚举描述
    /// </summary>
    /// <remarks>
    /// 未标注任何描述特性的字段，描述本身就等于字段名，这是没有资源文件时的最终兜底形态。
    /// </remarks>
    [Fact]
    public void Get_ByType_WhenNoResourceConfigured_FallsBackToDescription()
    {
        var service = CreateService();

        var definition = service.Get(typeof(LocalizationTestOrderStatus), ChineseQuery());

        Assert.Equal("待处理原始描述", SingleItem(definition, nameof(LocalizationTestOrderStatus.Pending)).Label);
        Assert.Equal("已支付原始描述", SingleItem(definition, nameof(LocalizationTestOrderStatus.Paid)).Label);
        Assert.Equal(nameof(LocalizationTestOrderStatus.Shipped), SingleItem(definition, nameof(LocalizationTestOrderStatus.Shipped)).Label);
    }

    /// <summary>
    /// 类型级本地化特性同时决定资源名与键前缀
    /// </summary>
    [Fact]
    public void Get_ByType_WhenTypeDeclaresResourceAttribute_UsesDeclaredResourceAndKeyPrefix()
    {
        _fileSystem.AddFile(
            "/Localization/LocalizationTestShopResource.zh-CN.json",
            """{"Shop.LocalizationTestShopStatus.Open":"营业中"}""");
        var service = CreateService();

        var definition = service.Get(typeof(LocalizationTestShopStatus), ChineseQuery());
        var open = SingleItem(definition, nameof(LocalizationTestShopStatus.Open));

        Assert.Equal("LocalizationTestShopResource", definition.ResourceName);
        Assert.Equal("LocalizationTestShopResource", open.ResourceName);
        Assert.Equal("营业中", open.Label);
        Assert.Equal("Shop.LocalizationTestShopStatus.Open", open.LocalizationKey);
    }

    /// <summary>
    /// 选项级键前缀会作为候选键之一被尝试
    /// </summary>
    [Fact]
    public void Get_ByType_WhenOptionsDeclareKeyPrefix_TriesPrefixedKey()
    {
        _optionsMonitor.CurrentValue.EnumLocalizationKeyPrefix = "App";
        _fileSystem.AddFile(
            "/Localization/Enums.zh-CN.json",
            """{"App.LocalizationTestPlainState.Draft":"草稿"}""");
        var service = CreateService();

        var item = SingleItem(service.Get(typeof(LocalizationTestPlainState), ChineseQuery()), nameof(LocalizationTestPlainState.Draft));

        Assert.Equal("草稿", item.Label);
        Assert.Equal("App.LocalizationTestPlainState.Draft", item.LocalizationKey);
    }

    /// <summary>
    /// 默认不返回隐藏项
    /// </summary>
    [Fact]
    public void Get_ByType_ByDefault_ExcludesHiddenItems()
    {
        var service = CreateService();

        var names = service.Get(typeof(LocalizationTestVisibility), ChineseQuery()).Items.Select(x => x.Name).ToList();

        Assert.DoesNotContain(nameof(LocalizationTestVisibility.Gamma), names);
        Assert.Contains(nameof(LocalizationTestVisibility.Alpha), names);
    }

    /// <summary>
    /// 显式要求包含隐藏项时返回隐藏项并标记 Hidden
    /// </summary>
    [Fact]
    public void Get_ByType_WhenIncludeHidden_ReturnsHiddenItems()
    {
        var service = CreateService();

        var definition = service.Get(
            typeof(LocalizationTestVisibility),
            new EnumLocalizationQuery { CultureName = "zh-CN", IncludeHidden = true });

        var gamma = SingleItem(definition, nameof(LocalizationTestVisibility.Gamma));
        Assert.True(gamma.Hidden);
    }

    /// <summary>
    /// 排序模式下按排序值升序返回
    /// </summary>
    [Fact]
    public void Get_ByType_WhenOrdered_SortsByOrderValue()
    {
        var service = CreateService();

        var names = service.Get(typeof(LocalizationTestVisibility), ChineseQuery()).Items.Select(x => x.Name).ToList();

        Assert.Equal(
            new[]
            {
                nameof(LocalizationTestVisibility.Alpha),
                nameof(LocalizationTestVisibility.Beta),
                nameof(LocalizationTestVisibility.Delta)
            },
            names);
    }

    /// <summary>
    /// 枚举项透传主题、图标、排序与禁用标记
    /// </summary>
    [Fact]
    public void Get_ByType_ItemsExposePresentationMetadata()
    {
        var service = CreateService();

        var definition = service.Get(typeof(LocalizationTestVisibility), ChineseQuery());
        var beta = SingleItem(definition, nameof(LocalizationTestVisibility.Beta));
        var delta = SingleItem(definition, nameof(LocalizationTestVisibility.Delta));

        Assert.Equal("warning", beta.Theme);
        Assert.Equal("eye", beta.Icon);
        Assert.Equal(2, beta.Order);
        Assert.False(beta.Disabled);
        Assert.True(delta.Disabled);
        Assert.NotNull(beta.Extra);
        Assert.True(beta.Extra!.ContainsKey("Icon"));
    }

    /// <summary>
    /// 枚举项的值就是枚举字段的原始值
    /// </summary>
    [Fact]
    public void Get_ByType_ItemValue_MatchesEnumFieldValue()
    {
        var service = CreateService();

        var item = SingleItem(service.Get(typeof(LocalizationTestOrderStatus), ChineseQuery()), nameof(LocalizationTestOrderStatus.Paid));

        Assert.Equal((object)LocalizationTestOrderStatus.Paid, item.Value);
    }

    /// <summary>
    /// 标志位枚举的定义级元数据
    /// </summary>
    [Fact]
    public void Get_ByType_FlagsEnum_ReportsIsFlagsAndTypeDescription()
    {
        var service = CreateService();

        var definition = service.Get(typeof(LocalizationTestPermission), ChineseQuery());

        Assert.True(definition.IsFlags);
        Assert.Equal("测试权限集合", definition.DisplayName);
    }

    /// <summary>
    /// 按短名解析枚举类型
    /// </summary>
    [Fact]
    public void Get_ByName_WithShortName_ResolvesEnumType()
    {
        var service = CreateService();

        var definition = service.Get(nameof(LocalizationTestOrderStatus), ChineseQuery());

        Assert.Equal(typeof(LocalizationTestOrderStatus).FullName, definition.FullName);
    }

    /// <summary>
    /// 按完整名解析枚举类型
    /// </summary>
    [Fact]
    public void Get_ByName_WithFullName_ResolvesEnumType()
    {
        var service = CreateService();

        var definition = service.Get(typeof(LocalizationTestOrderStatus).FullName!, ChineseQuery());

        Assert.Equal(nameof(LocalizationTestOrderStatus), definition.EnumName);
    }

    /// <summary>
    /// 名称首尾空白被裁剪后仍能解析
    /// </summary>
    [Fact]
    public void Get_ByName_TrimsSurroundingWhitespace()
    {
        var service = CreateService();

        var definition = service.Get($"  {nameof(LocalizationTestOrderStatus)}  ", ChineseQuery());

        Assert.Equal(nameof(LocalizationTestOrderStatus), definition.EnumName);
    }

    /// <summary>
    /// 未知枚举名抛未找到键异常
    /// </summary>
    [Fact]
    public void Get_ByName_WhenUnknown_ThrowsKeyNotFoundException()
    {
        var service = CreateService();

        Assert.Throws<KeyNotFoundException>(() =>
        {
            _ = service.Get("LocalizationTestNoSuchEnumTypeName");
        });
    }

    /// <summary>
    /// 枚举名为空白时抛参数异常
    /// </summary>
    /// <param name="enumTypeName">枚举类型名</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Get_ByName_WhenBlank_ThrowsArgumentException(string enumTypeName)
    {
        var service = CreateService();

        Assert.Throws<ArgumentException>(() =>
        {
            _ = service.Get(enumTypeName);
        });
    }

    /// <summary>
    /// 指定文化名时按该文化取文本
    /// </summary>
    [Fact]
    public void Get_WhenCultureNameSpecified_UsesRequestedCulture()
    {
        AddEnumResources();
        _fileSystem.AddFile(
            "/Localization/Enums.en-US.json",
            """{"LocalizationTestOrderStatus.Pending":"Pending!"}""");
        var service = CreateService();

        var definition = service.Get(
            typeof(LocalizationTestOrderStatus),
            new EnumLocalizationQuery { CultureName = "en-US" });

        Assert.Equal("en-US", definition.CultureName);
        Assert.Equal("Pending!", SingleItem(definition, nameof(LocalizationTestOrderStatus.Pending)).Label);
    }

    /// <summary>
    /// 文化名非法时回退到当前 UI 文化
    /// </summary>
    [Fact]
    public void Get_WhenCultureNameInvalid_FallsBackToAmbientUiCulture()
    {
        var service = CreateService();

        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

            var definition = service.Get(
                typeof(LocalizationTestOrderStatus),
                new EnumLocalizationQuery { CultureName = "!!not-a-culture!!" });

            Assert.Equal("en-US", definition.CultureName);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    /// <summary>
    /// 已知枚举名时 TryGet 返回 true 并给出结果
    /// </summary>
    [Fact]
    public void TryGet_WhenKnown_ReturnsTrueWithResult()
    {
        var service = CreateService();

        var success = service.TryGet(nameof(LocalizationTestOrderStatus), out var result, ChineseQuery());

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal(nameof(LocalizationTestOrderStatus), result!.EnumName);
    }

    /// <summary>
    /// 未知枚举名时 TryGet 返回 false 且结果为空
    /// </summary>
    [Fact]
    public void TryGet_WhenUnknown_ReturnsFalseWithNullResult()
    {
        var service = CreateService();

        var success = service.TryGet("LocalizationTestNoSuchEnumTypeName", out var result);

        Assert.False(success);
        Assert.Null(result);
    }

    /// <summary>
    /// 枚举名为空白时 TryGet 返回 false 且不抛异常
    /// </summary>
    /// <param name="enumTypeName">枚举类型名</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryGet_WhenBlank_ReturnsFalseWithNullResult(string enumTypeName)
    {
        var service = CreateService();

        var success = service.TryGet(enumTypeName, out var result);

        Assert.False(success);
        Assert.Null(result);
    }

    /// <summary>
    /// 批量读取按不区分大小写去重并跳过空白名称
    /// </summary>
    [Fact]
    public void GetMany_DeduplicatesIgnoringCaseAndSkipsBlankNames()
    {
        var service = CreateService();

        var result = service.GetMany(
            [
                nameof(LocalizationTestOrderStatus),
                nameof(LocalizationTestOrderStatus).ToUpperInvariant(),
                "   ",
                nameof(LocalizationTestPermission)
            ],
            ChineseQuery());

        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey(nameof(LocalizationTestOrderStatus)));
        Assert.True(result.ContainsKey(nameof(LocalizationTestPermission)));
    }

    /// <summary>
    /// 批量读取的名称集合为空时抛参数空异常
    /// </summary>
    [Fact]
    public void GetMany_WhenNamesNull_ThrowsArgumentNullException()
    {
        var service = CreateService();

        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = service.GetMany(null!);
        });
    }

    private static EnumLocalizationQuery ChineseQuery()
    {
        return new EnumLocalizationQuery { CultureName = "zh-CN" };
    }

    private static LocalizedEnumItem SingleItem(LocalizedEnumDefinition definition, string name)
    {
        return Assert.Single(definition.Items, item => item.Name == name);
    }

    private void AddEnumResources()
    {
        _fileSystem.AddFile(
            "/Localization/Enums.zh-CN.json",
            """
            {
                "LocalizationTestOrderStatus.Pending": "待处理",
                "LocalizationTestOrderStatus_Paid": "已支付",
                "Shipped": "已发货",
                "LocalizationTestOrderStatus.Cancelled": "   "
            }
            """);
    }

    private EnumLocalizationService CreateService()
    {
        var store = new JsonLocalizationResourceStore(_fileSystem, _optionsMonitor);
        _stores.Add(store);
        return new EnumLocalizationService(store, _optionsMonitor);
    }
}
