// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Localization;
using XiHan.Framework.Localization.Abstractions.Tests.Fakes;

namespace XiHan.Framework.Localization.Abstractions.Tests;

/// <summary>
/// 可本地化字符串扩展方法测试
/// </summary>
/// <remarks>
/// 三个扩展方法的取舍规则很细：回退值只有在"资源缺失且回退值非空白"时才生效，
/// 资源命中时即使给了回退值也必须用本地化结果；这些分支是最容易被改坏的地方，逐条锁死。
/// </remarks>
public class LocalizableStringExtensionsTests
{
    /// <summary>
    /// 源为 null 时直接返回回退值
    /// </summary>
    [Fact]
    public void LocalizeOrFallback_WhenSourceNull_ReturnsFallback()
    {
        var factory = new FakeStringLocalizerFactory();

        var result = LocalizableStringExtensions.LocalizeOrFallback(null, factory, "回退值");

        Assert.Equal("回退值", result);
    }

    /// <summary>
    /// 源为 null 且未给回退值时返回空串而不是 null
    /// </summary>
    [Fact]
    public void LocalizeOrFallback_WhenSourceNullAndFallbackNull_ReturnsEmptyString()
    {
        var factory = new FakeStringLocalizerFactory();

        var result = LocalizableStringExtensions.LocalizeOrFallback(null, factory);

        Assert.Equal(string.Empty, result);
    }

    /// <summary>
    /// 工厂校验发生在源判空之前，源为 null 也必须先抛工厂参数异常
    /// </summary>
    [Fact]
    public void LocalizeOrFallback_WhenFactoryNull_ThrowsBeforeSourceNullCheck()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => LocalizableStringExtensions.LocalizeOrFallback(null, null!, "回退值"));

        Assert.Equal("stringLocalizerFactory", exception.ParamName);
    }

    /// <summary>
    /// 资源命中时返回本地化结果，回退值必须被忽略
    /// </summary>
    [Fact]
    public void LocalizeOrFallback_WhenResourceFound_IgnoresFallback()
    {
        var factory = new FakeStringLocalizerFactory();
        var localizable = FakeLocalizableString.Found("Title", "标题");

        var result = localizable.LocalizeOrFallback(factory, "回退值");

        Assert.Equal("标题", result);
    }

    /// <summary>
    /// 资源缺失且回退值有效时使用回退值
    /// </summary>
    [Fact]
    public void LocalizeOrFallback_WhenResourceMissing_ReturnsFallback()
    {
        var factory = new FakeStringLocalizerFactory();
        var localizable = FakeLocalizableString.Missing("Title");

        var result = localizable.LocalizeOrFallback(factory, "回退值");

        Assert.Equal("回退值", result);
    }

    /// <summary>
    /// 资源缺失但回退值为空或纯空白时，仍返回本地化结果（通常是资源键本身）
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void LocalizeOrFallback_WhenResourceMissingAndFallbackBlank_ReturnsLocalizedValue(string? fallback)
    {
        var factory = new FakeStringLocalizerFactory();
        var localizable = FakeLocalizableString.Missing("Title");

        var result = localizable.LocalizeOrFallback(factory, fallback);

        Assert.Equal("Title", result);
    }

    /// <summary>
    /// 本地化工厂必须原样透传给底层可本地化对象
    /// </summary>
    [Fact]
    public void LocalizeOrFallback_PassesFactoryThroughToSource()
    {
        var factory = new FakeStringLocalizerFactory();
        var localizable = FakeLocalizableString.Found("Title", "标题");

        _ = localizable.LocalizeOrFallback(factory);

        Assert.Equal(1, localizable.LocalizeCallCount);
        Assert.Same(factory, localizable.LastFactory);
    }

    /// <summary>
    /// 显示名为空时回落到标识名称
    /// </summary>
    [Fact]
    public void GetLocalizedDisplayName_WhenDisplayNameNull_ReturnsName()
    {
        var factory = new FakeStringLocalizerFactory();
        var source = new FakeNamedDisplayObject("UserManagement");

        var result = source.GetLocalizedDisplayName(factory);

        Assert.Equal("UserManagement", result);
    }

    /// <summary>
    /// 显示名可本地化时返回本地化文本
    /// </summary>
    [Fact]
    public void GetLocalizedDisplayName_WhenDisplayNameResolved_ReturnsLocalizedValue()
    {
        var factory = new FakeStringLocalizerFactory();
        var source = new FakeNamedDisplayObject("UserManagement", FakeLocalizableString.Found("Menu.User", "用户管理"));

        var result = source.GetLocalizedDisplayName(factory);

        Assert.Equal("用户管理", result);
    }

    /// <summary>
    /// 显示名对应资源缺失时回落到标识名称，而不是资源键
    /// </summary>
    [Fact]
    public void GetLocalizedDisplayName_WhenDisplayNameResourceMissing_FallsBackToName()
    {
        var factory = new FakeStringLocalizerFactory();
        var source = new FakeNamedDisplayObject("UserManagement", FakeLocalizableString.Missing("Menu.User"));

        var result = source.GetLocalizedDisplayName(factory);

        Assert.Equal("UserManagement", result);
    }

    /// <summary>
    /// 显示名资源缺失且标识名称也是空白时，退回本地化结果本身
    /// </summary>
    [Fact]
    public void GetLocalizedDisplayName_WhenNameBlankAndResourceMissing_ReturnsLocalizedValue()
    {
        var factory = new FakeStringLocalizerFactory();
        var source = new FakeNamedDisplayObject("   ", FakeLocalizableString.Missing("Menu.User"));

        var result = source.GetLocalizedDisplayName(factory);

        Assert.Equal("Menu.User", result);
    }

    /// <summary>
    /// 源对象为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void GetLocalizedDisplayName_WhenSourceNull_ThrowsArgumentNullException()
    {
        var factory = new FakeStringLocalizerFactory();

        var exception = Assert.Throws<ArgumentNullException>(
            () => LocalizableStringExtensions.GetLocalizedDisplayName(null!, factory));

        Assert.Equal("source", exception.ParamName);
    }

    /// <summary>
    /// 本地化工厂为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void GetLocalizedDisplayName_WhenFactoryNull_ThrowsArgumentNullException()
    {
        var source = new FakeNamedDisplayObject("UserManagement");

        var exception = Assert.Throws<ArgumentNullException>(() => source.GetLocalizedDisplayName(null!));

        Assert.Equal("stringLocalizerFactory", exception.ParamName);
    }

    /// <summary>
    /// 字符串转固定文本本地化字符串后保留原值
    /// </summary>
    [Fact]
    public void ToFixedLocalizableString_KeepsOriginalValue()
    {
        var result = "直接显示的文本".ToFixedLocalizableString();

        var fixedString = Assert.IsType<FixedLocalizableString>(result);
        Assert.Equal("直接显示的文本", fixedString.Value);
    }

    /// <summary>
    /// 转换结果本地化后仍是原文本
    /// </summary>
    [Fact]
    public void ToFixedLocalizableString_LocalizesToOriginalValue()
    {
        var factory = new FakeStringLocalizerFactory();

        var localized = "直接显示的文本".ToFixedLocalizableString().Localize(factory);

        Assert.Equal("直接显示的文本", localized.Value);
        Assert.False(localized.ResourceNotFound);
    }

    /// <summary>
    /// 对 null 字符串调用转换必须抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void ToFixedLocalizableString_WhenValueNull_ThrowsArgumentNullException()
    {
        string value = null!;

        var exception = Assert.Throws<ArgumentNullException>(() => value.ToFixedLocalizableString());

        Assert.Equal("value", exception.ParamName);
    }

    /// <summary>
    /// 资源缺失标记必须来自底层结果而不是"文本等于资源键"的猜测
    /// </summary>
    [Fact]
    public void LocalizeOrFallback_WhenValueEqualsKeyButResourceFound_DoesNotUseFallback()
    {
        var factory = new FakeStringLocalizerFactory();

        // 本地化文本恰好与资源键相同，但资源确实命中，此时不能误判为缺失而走回退
        var localizable = new FakeLocalizableString(new LocalizedString("Title", "Title", resourceNotFound: false));

        var result = localizable.LocalizeOrFallback(factory, "回退值");

        Assert.Equal("Title", result);
    }
}
