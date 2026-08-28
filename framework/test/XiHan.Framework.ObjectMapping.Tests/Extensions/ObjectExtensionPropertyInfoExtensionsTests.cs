// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using XiHan.Framework.ObjectMapping.Extensions;
using XiHan.Framework.ObjectMapping.Tests.Fakes;

namespace XiHan.Framework.ObjectMapping.Tests.Extensions;

/// <summary>
/// 对象扩展属性信息扩展方法测试
/// </summary>
/// <remarks>
/// GetValidationAttributes 是验证器唯一的取特性入口：它从混杂的 Attributes 列表里筛出
/// ValidationAttribute 子类，非验证特性（例如 Description）必须被过滤掉，
/// 否则验证阶段会把无关元数据当成校验规则。
/// </remarks>
public class ObjectExtensionPropertyInfoExtensionsTests
{
    /// <summary>
    /// 没有任何特性时返回空数组而不是 null
    /// </summary>
    [Fact]
    public void GetValidationAttributes_WhenNoAttribute_ReturnsEmptyArray()
    {
        var sut = CreateProperty(typeof(string));

        var attributes = sut.GetValidationAttributes();

        Assert.NotNull(attributes);
        Assert.Empty(attributes);
    }

    /// <summary>
    /// 非空基元类型自动补的 Required 特性会被取到
    /// </summary>
    [Fact]
    public void GetValidationAttributes_IncludesAutomaticallyAddedRequiredAttribute()
    {
        var sut = CreateProperty(typeof(int));

        var attributes = sut.GetValidationAttributes();

        Assert.Single(attributes);
        Assert.IsType<RequiredAttribute>(attributes[0]);
    }

    /// <summary>
    /// 非验证特性被过滤掉
    /// </summary>
    [Fact]
    public void GetValidationAttributes_FiltersOutNonValidationAttributes()
    {
        var sut = CreateProperty(typeof(string));
        sut.Attributes.Add(new DescriptionAttribute("只是描述"));
        sut.Attributes.Add(new StringLengthAttribute(10));

        var attributes = sut.GetValidationAttributes();

        Assert.Single(attributes);
        Assert.IsType<StringLengthAttribute>(attributes[0]);
    }

    /// <summary>
    /// 多个验证特性按加入顺序返回
    /// </summary>
    [Fact]
    public void GetValidationAttributes_KeepsDeclarationOrder()
    {
        var sut = CreateProperty(typeof(string));
        sut.Attributes.Add(new RequiredAttribute());
        sut.Attributes.Add(new StringLengthAttribute(10));
        sut.Attributes.Add(new RegularExpressionAttribute("^[a-z]+$"));

        var attributes = sut.GetValidationAttributes();

        Assert.Equal(3, attributes.Length);
        Assert.IsType<RequiredAttribute>(attributes[0]);
        Assert.IsType<StringLengthAttribute>(attributes[1]);
        Assert.IsType<RegularExpressionAttribute>(attributes[2]);
    }

    /// <summary>
    /// 每次调用返回新的数组，改动结果不会回写到属性信息上
    /// </summary>
    [Fact]
    public void GetValidationAttributes_ReturnsDetachedSnapshot()
    {
        var sut = CreateProperty(typeof(int));

        var first = sut.GetValidationAttributes();
        sut.Attributes.Add(new StringLengthAttribute(10));
        var second = sut.GetValidationAttributes();

        Assert.NotSame(first, second);
        Assert.Single(first);
        Assert.Equal(2, second.Length);
    }

    /// <summary>
    /// 创建一个挂在临时宿主上的扩展属性信息
    /// </summary>
    /// <param name="propertyType">属性类型</param>
    /// <returns>扩展属性信息</returns>
    private static ObjectExtensionPropertyInfo CreateProperty(Type propertyType)
    {
        var owner = new ObjectExtensionInfo(typeof(FakeExtensibleObject));
        return new ObjectExtensionPropertyInfo(owner, propertyType, "Property");
    }
}
