// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Core.Tests.Modularity;

/// <summary>
/// 附加程序集特性测试
/// </summary>
/// <remarks>
/// 特性以「程序集里的任意类型」间接指代程序集，多个类型落在同一程序集时必须去重，
/// 否则模块描述器的 AllAssemblies 会重复扫描同一份程序集。
/// </remarks>
public class AdditionalAssemblyAttributeTests
{
    /// <summary>
    /// 按声明类型解析出其所在程序集
    /// </summary>
    [Fact]
    public void GetAssemblies_ResolvesAssemblyOfDeclaredType()
    {
        var attribute = new AdditionalAssemblyAttribute(typeof(XiHanModule));

        Assert.Equal(typeof(XiHanModule).Assembly, Assert.Single(attribute.GetAssemblies()));
    }

    /// <summary>
    /// 同一程序集的多个类型只产出一份程序集
    /// </summary>
    [Fact]
    public void GetAssemblies_WhenTypesShareAssembly_Deduplicates()
    {
        var attribute = new AdditionalAssemblyAttribute(typeof(XiHanModule), typeof(XiHanModuleDescriptor));

        Assert.Single(attribute.GetAssemblies());
    }

    /// <summary>
    /// 不同程序集的类型各产出一份
    /// </summary>
    [Fact]
    public void GetAssemblies_WhenTypesFromDifferentAssemblies_ReturnsEach()
    {
        var attribute = new AdditionalAssemblyAttribute(typeof(XiHanModule), typeof(AdditionalAssemblyAttributeTests));

        var assemblies = attribute.GetAssemblies();

        Assert.Equal(2, assemblies.Length);
        Assert.Contains(typeof(XiHanModule).Assembly, assemblies);
        Assert.Contains(typeof(AdditionalAssemblyAttributeTests).Assembly, assemblies);
    }

    /// <summary>
    /// 无参构造时不产出任何程序集
    /// </summary>
    [Fact]
    public void Constructor_WhenNoArgument_ReturnsEmpty()
    {
        var attribute = new AdditionalAssemblyAttribute();

        Assert.Empty(attribute.TypesInAssemblies);
        Assert.Empty(attribute.GetAssemblies());
    }

    /// <summary>
    /// 传入空数组引用时退化为空类型集合
    /// </summary>
    [Fact]
    public void Constructor_WhenNullArray_ReturnsEmptyTypes()
    {
        var attribute = new AdditionalAssemblyAttribute((Type[]?)null);

        Assert.Same(Type.EmptyTypes, attribute.TypesInAssemblies);
    }

    /// <summary>
    /// 特性实现附加程序集提供器契约
    /// </summary>
    [Fact]
    public void Attribute_ImplementsAdditionalModuleAssemblyProvider()
    {
        IAdditionalModuleAssemblyProvider provider = new AdditionalAssemblyAttribute(typeof(XiHanModule));

        Assert.Equal(typeof(XiHanModule).Assembly, Assert.Single(provider.GetAssemblies()));
    }
}
