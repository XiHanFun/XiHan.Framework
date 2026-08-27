// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Core.Tests.Modularity;

/// <summary>
/// 类型依赖特性测试
/// </summary>
/// <remarks>
/// 依赖特性是模块依赖图的唯一声明来源，允许同一模块上叠加多个特性，
/// 且构造参数传空时必须退化为空依赖而不是抛空引用——模块发现阶段会对每个模块无差别调用。
/// </remarks>
public class DependsOnAttributeTests
{
    /// <summary>
    /// 声明的依赖类型按顺序保留
    /// </summary>
    [Fact]
    public void GetDependedTypes_KeepsDeclarationOrder()
    {
        var attribute = new DependsOnAttribute(typeof(DoaFirstModule), typeof(DoaSecondModule));

        var depended = attribute.GetDependedTypes();

        Assert.Equal(2, depended.Length);
        Assert.Equal(typeof(DoaFirstModule), depended[0]);
        Assert.Equal(typeof(DoaSecondModule), depended[1]);
        Assert.Same(attribute.DependedTypes, depended);
    }

    /// <summary>
    /// 无参构造时依赖为空
    /// </summary>
    [Fact]
    public void Constructor_WhenNoArgument_ReturnsEmptyDependencies()
    {
        var attribute = new DependsOnAttribute();

        Assert.Empty(attribute.GetDependedTypes());
    }

    /// <summary>
    /// 传入空数组引用时退化为空依赖
    /// </summary>
    [Fact]
    public void Constructor_WhenNullArray_ReturnsEmptyDependencies()
    {
        var attribute = new DependsOnAttribute((Type[]?)null);

        Assert.Same(Type.EmptyTypes, attribute.DependedTypes);
    }

    /// <summary>
    /// 同一模块上可叠加多个依赖特性
    /// </summary>
    [Fact]
    public void Attribute_AllowsMultipleDeclarationsOnSameType()
    {
        var attributes = typeof(DoaMultiDependsModule).GetCustomAttributes<DependsOnAttribute>(false).ToList();

        Assert.Equal(2, attributes.Count);
        Assert.Contains(attributes, a => a.DependedTypes.Contains(typeof(DoaFirstModule)));
        Assert.Contains(attributes, a => a.DependedTypes.Contains(typeof(DoaSecondModule)));
    }

    /// <summary>
    /// 特性实现依赖类型提供器契约
    /// </summary>
    [Fact]
    public void Attribute_ImplementsDependedTypesProvider()
    {
        IDependedTypesProvider provider = new DependsOnAttribute(typeof(DoaFirstModule));

        Assert.Equal(typeof(DoaFirstModule), Assert.Single(provider.GetDependedTypes()));
    }
}

/// <summary>
/// 依赖特性测试用模块甲
/// </summary>
internal class DoaFirstModule : XiHanModule;

/// <summary>
/// 依赖特性测试用模块乙
/// </summary>
internal class DoaSecondModule : XiHanModule;

/// <summary>
/// 叠加多个依赖特性的模块
/// </summary>
[DependsOn(typeof(DoaFirstModule))]
[DependsOn(typeof(DoaSecondModule))]
internal class DoaMultiDependsModule : XiHanModule;
