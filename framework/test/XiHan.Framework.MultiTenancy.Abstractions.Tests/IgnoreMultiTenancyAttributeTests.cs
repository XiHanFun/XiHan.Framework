// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.MultiTenancy.Abstractions.Tests;

/// <summary>
/// 忽略多租户特性的测试
/// </summary>
/// <remarks>
/// 这个特性是「关掉租户过滤」的开关，框架侧靠反射读取它，所以它的元数据本身就是契约：
/// 作用目标必须覆盖到类/属性/方法，是否可继承、是否可重复也必须锁死——
/// 一旦 Inherited 从 true 变成 false，所有靠基类打标记来豁免的派生实体都会在某次升级后悄无声息地重新被过滤。
/// </remarks>
public class IgnoreMultiTenancyAttributeTests
{
    /// <summary>
    /// 特性可作用于任意程序元素
    /// </summary>
    [Fact]
    public void AttributeUsage_TargetsAllProgramElements()
    {
        var usage = typeof(IgnoreMultiTenancyAttribute).GetCustomAttribute<AttributeUsageAttribute>(false);

        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.All, usage.ValidOn);
    }

    /// <summary>
    /// 特性可被继承且不允许在同一元素上重复标注
    /// </summary>
    [Fact]
    public void AttributeUsage_IsInheritedAndSingleUse()
    {
        var usage = typeof(IgnoreMultiTenancyAttribute).GetCustomAttribute<AttributeUsageAttribute>(false);

        Assert.NotNull(usage);
        Assert.True(usage.Inherited);
        Assert.False(usage.AllowMultiple);
    }

    /// <summary>
    /// 特性直接派生自 Attribute，可被通用反射扫描发现
    /// </summary>
    [Fact]
    public void Attribute_DerivesFromAttribute_AndIsPublic()
    {
        Assert.Equal(typeof(Attribute), typeof(IgnoreMultiTenancyAttribute).BaseType);
        Assert.True(typeof(IgnoreMultiTenancyAttribute).IsPublic);
        Assert.False(typeof(IgnoreMultiTenancyAttribute).IsAbstract);
    }

    /// <summary>
    /// 标注在类型上时可被反射读到
    /// </summary>
    [Fact]
    public void Attribute_OnType_IsDiscoverable()
    {
        var attribute = typeof(AnnotatedBase).GetCustomAttribute<IgnoreMultiTenancyAttribute>(false);

        Assert.NotNull(attribute);
    }

    /// <summary>
    /// 基类上的标记会被派生类继承
    /// </summary>
    /// <remarks>
    /// 派生类自身没有标记，只有开启继承查找才能读到，这条正是 Inherited = true 的实际效果。
    /// </remarks>
    [Fact]
    public void Attribute_OnBaseType_IsInheritedByDerivedType()
    {
        Assert.NotNull(typeof(AnnotatedDerived).GetCustomAttribute<IgnoreMultiTenancyAttribute>(true));
        Assert.Null(typeof(AnnotatedDerived).GetCustomAttribute<IgnoreMultiTenancyAttribute>(false));
    }

    /// <summary>
    /// 标注在属性与方法上时同样可被反射读到
    /// </summary>
    [Fact]
    public void Attribute_OnMembers_IsDiscoverable()
    {
        var property = typeof(AnnotatedMembers).GetProperty(nameof(AnnotatedMembers.IgnoredProperty));
        var method = typeof(AnnotatedMembers).GetMethod(nameof(AnnotatedMembers.IgnoredMethod));

        Assert.NotNull(property);
        Assert.NotNull(method);
        Assert.NotNull(property.GetCustomAttribute<IgnoreMultiTenancyAttribute>(false));
        Assert.NotNull(method.GetCustomAttribute<IgnoreMultiTenancyAttribute>(false));
    }

    /// <summary>
    /// 未标注的类型读不到特性
    /// </summary>
    [Fact]
    public void Attribute_OnUnannotatedType_IsNotFound()
    {
        Assert.Null(typeof(PlainType).GetCustomAttribute<IgnoreMultiTenancyAttribute>(true));
    }

    /// <summary>
    /// 打了忽略多租户标记的基类
    /// </summary>
    [IgnoreMultiTenancy]
    private class AnnotatedBase
    {
    }

    /// <summary>
    /// 自身未打标记、依靠继承取得标记的派生类
    /// </summary>
    private sealed class AnnotatedDerived : AnnotatedBase
    {
    }

    /// <summary>
    /// 在成员级别打了忽略多租户标记的类型
    /// </summary>
    private sealed class AnnotatedMembers
    {
        /// <summary>
        /// 打了标记的属性
        /// </summary>
        [IgnoreMultiTenancy]
        public string? IgnoredProperty { get; set; }

        /// <summary>
        /// 打了标记的方法
        /// </summary>
        [IgnoreMultiTenancy]
        public void IgnoredMethod()
        {
        }
    }

    /// <summary>
    /// 完全未打标记的类型
    /// </summary>
    private sealed class PlainType
    {
    }
}
