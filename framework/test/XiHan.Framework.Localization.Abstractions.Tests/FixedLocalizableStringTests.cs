// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Localization.Abstractions.Tests.Fakes;

namespace XiHan.Framework.Localization.Abstractions.Tests;

/// <summary>
/// 固定文本本地化字符串测试
/// </summary>
/// <remarks>
/// 该类型的契约是"完全绕开本地化管线"：无论传入什么工厂，都必须原样返回构造时的文本，
/// 因此除了返回值本身，还要断言工厂一次都没有被使用。
/// </remarks>
public class FixedLocalizableStringTests
{
    /// <summary>
    /// 构造时传入 null 文本必须抛出 ArgumentNullException 且参数名为 value
    /// </summary>
    [Fact]
    public void Constructor_WhenValueNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new FixedLocalizableString(null!));

        Assert.Equal("value", exception.ParamName);
    }

    /// <summary>
    /// 空串与纯空白是合法固定文本，不做非空校验
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("固定文本")]
    public void Constructor_WhenValueNotNull_KeepsValueAsIs(string value)
    {
        var sut = new FixedLocalizableString(value);

        Assert.Equal(value, sut.Value);
    }

    /// <summary>
    /// 本地化结果的资源键与文本都等于固定文本，且标记为资源已命中
    /// </summary>
    [Fact]
    public void Localize_ReturnsFixedTextAsBothNameAndValue()
    {
        var sut = new FixedLocalizableString("直接显示的文本");
        var factory = new FakeStringLocalizerFactory();

        var localized = sut.Localize(factory);

        Assert.Equal("直接显示的文本", localized.Name);
        Assert.Equal("直接显示的文本", localized.Value);
        Assert.False(localized.ResourceNotFound);
    }

    /// <summary>
    /// 本地化过程不得触碰本地化工厂
    /// </summary>
    [Fact]
    public void Localize_DoesNotCreateAnyLocalizer()
    {
        var sut = new FixedLocalizableString("直接显示的文本");
        var factory = new FakeStringLocalizerFactory();

        _ = sut.Localize(factory);

        Assert.Equal(0, factory.CreateCallCount);
    }

    /// <summary>
    /// 工厂为 null 时必须抛出 ArgumentNullException，即使结果并不依赖工厂
    /// </summary>
    [Fact]
    public void Localize_WhenFactoryNull_ThrowsArgumentNullException()
    {
        var sut = new FixedLocalizableString("直接显示的文本");

        var exception = Assert.Throws<ArgumentNullException>(() => sut.Localize(null!));

        Assert.Equal("stringLocalizerFactory", exception.ParamName);
    }

    /// <summary>
    /// ToString 返回固定文本本身
    /// </summary>
    [Fact]
    public void ToString_ReturnsFixedText()
    {
        var sut = new FixedLocalizableString("直接显示的文本");

        Assert.Equal("直接显示的文本", sut.ToString());
    }

    /// <summary>
    /// 该类型必须可作为 ILocalizableString 使用
    /// </summary>
    [Fact]
    public void Type_ImplementsLocalizableStringContract()
    {
        var sut = new FixedLocalizableString("直接显示的文本");

        Assert.IsAssignableFrom<ILocalizableString>(sut);
    }
}
