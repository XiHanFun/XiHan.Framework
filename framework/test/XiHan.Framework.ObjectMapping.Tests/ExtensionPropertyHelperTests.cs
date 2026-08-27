// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.ObjectMapping.Tests.Fakes;

namespace XiHan.Framework.ObjectMapping.Tests;

/// <summary>
/// 扩展属性帮助类测试
/// </summary>
/// <remarks>
/// GetDefaultAttributes 是迭代器方法，参数校验被推迟到首次枚举才执行，
/// 因此空引用用例必须显式枚举（ToList）才能观察到异常——这一点写测试时极易踩空。
/// </remarks>
public class ExtensionPropertyHelperTests
{
    /// <summary>
    /// 非空基元类型默认补一个 Required 特性
    /// </summary>
    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(long))]
    [InlineData(typeof(bool))]
    [InlineData(typeof(char))]
    [InlineData(typeof(double))]
    [InlineData(typeof(decimal))]
    [InlineData(typeof(Guid))]
    [InlineData(typeof(DateTime))]
    [InlineData(typeof(DateTimeOffset))]
    [InlineData(typeof(TimeSpan))]
    public void GetDefaultAttributes_ForNonNullablePrimitiveType_YieldsRequiredOnly(Type type)
    {
        var attributes = ExtensionPropertyHelper.GetDefaultAttributes(type).ToList();

        Assert.Single(attributes);
        Assert.IsType<RequiredAttribute>(attributes[0]);
    }

    /// <summary>
    /// 引用类型与可空基元类型不补任何特性
    /// </summary>
    [Theory]
    [InlineData(typeof(string))]
    [InlineData(typeof(object))]
    [InlineData(typeof(int?))]
    [InlineData(typeof(DateTime?))]
    public void GetDefaultAttributes_ForNullableOrReferenceType_YieldsNothing(Type type)
    {
        Assert.Empty(ExtensionPropertyHelper.GetDefaultAttributes(type).ToList());
    }

    /// <summary>
    /// 枚举类型补 Required 与 EnumDataType 两个特性，且顺序固定
    /// </summary>
    [Fact]
    public void GetDefaultAttributes_ForEnumType_YieldsRequiredThenEnumDataType()
    {
        var attributes = ExtensionPropertyHelper.GetDefaultAttributes(typeof(FakeExtensionEnum)).ToList();

        Assert.Equal(2, attributes.Count);
        Assert.IsType<RequiredAttribute>(attributes[0]);
        var enumAttribute = Assert.IsType<EnumDataTypeAttribute>(attributes[1]);
        Assert.Equal(typeof(FakeExtensionEnum), enumAttribute.EnumType);
    }

    /// <summary>
    /// 可空枚举不被识别为枚举，不补任何特性
    /// </summary>
    [Fact]
    public void GetDefaultAttributes_ForNullableEnum_YieldsNothing()
    {
        Assert.Empty(ExtensionPropertyHelper.GetDefaultAttributes(typeof(FakeExtensionEnum?)).ToList());
    }

    /// <summary>
    /// 类型为 null 时枚举序列会抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void GetDefaultAttributes_WhenTypeNull_ThrowsArgumentNullExceptionOnEnumeration()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => ExtensionPropertyHelper.GetDefaultAttributes(null!).ToList());

        Assert.Equal("type", exception.ParamName);
    }

    /// <summary>
    /// 没有工厂也没有指定默认值时回落到类型默认值
    /// </summary>
    [Fact]
    public void GetDefaultValue_WhenNothingGiven_ReturnsTypeDefault()
    {
        Assert.Equal(0, ExtensionPropertyHelper.GetDefaultValue(typeof(int), null, null));
        Assert.Equal(Guid.Empty, ExtensionPropertyHelper.GetDefaultValue(typeof(Guid), null, null));
        Assert.Null(ExtensionPropertyHelper.GetDefaultValue(typeof(string), null, null));
    }

    /// <summary>
    /// 指定了默认值且没有工厂时返回该默认值
    /// </summary>
    [Fact]
    public void GetDefaultValue_WhenDefaultValueGiven_ReturnsIt()
    {
        Assert.Equal(5, ExtensionPropertyHelper.GetDefaultValue(typeof(int), null, 5));
        Assert.Equal("默认名", ExtensionPropertyHelper.GetDefaultValue(typeof(string), null, "默认名"));
    }

    /// <summary>
    /// 工厂优先级最高，即使同时给了默认值也走工厂
    /// </summary>
    [Fact]
    public void GetDefaultValue_WhenFactoryGiven_TakesPrecedenceOverDefaultValue()
    {
        var result = ExtensionPropertyHelper.GetDefaultValue(typeof(string), () => "工厂值", "默认名");

        Assert.Equal("工厂值", result);
    }

    /// <summary>
    /// 工厂返回 null 时不会再回落到类型默认值
    /// </summary>
    /// <remarks>
    /// 工厂分支是提前 return，因此「工厂显式给出 null」是一个可表达的结果，
    /// 与「没有配置任何默认值」并不等价——对可空扩展属性来说这是有意义的区分。
    /// </remarks>
    [Fact]
    public void GetDefaultValue_WhenFactoryReturnsNull_DoesNotFallBackToTypeDefault()
    {
        var result = ExtensionPropertyHelper.GetDefaultValue(typeof(int), () => null!, 5);

        Assert.Null(result);
    }

    /// <summary>
    /// 属性类型为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void GetDefaultValue_WhenPropertyTypeNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => ExtensionPropertyHelper.GetDefaultValue(null!, null, null));

        Assert.Equal("propertyType", exception.ParamName);
    }
}
