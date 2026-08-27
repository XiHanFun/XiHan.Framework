// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.DependencyInjection;

namespace XiHan.Framework.Core.Tests.DependencyInjection;

/// <summary>
/// 暴露服务特性测试
/// </summary>
/// <remarks>
/// 覆盖 IncludeDefaults 与 IncludeSelf 的四种组合，以及默认服务的命名匹配规则
/// （接口去掉前导 I 后必须是实现类名的后缀，泛型接口按去掉反引号的名字参与匹配）。
/// 该规则决定了绝大多数框架服务在没有显式声明时暴露成什么，属高价值契约。
/// </remarks>
public class ExposeServicesAttributeTests
{
    /// <summary>
    /// 默认只暴露构造函数中声明的类型
    /// </summary>
    [Fact]
    public void GetExposedServiceTypes_WhenOnlyDeclaredTypes_ReturnsDeclaredTypesOnly()
    {
        var attribute = new ExposeServicesAttribute(typeof(IEsaNamed));

        var exposed = attribute.GetExposedServiceTypes(typeof(EsaNamed));

        Assert.Equal(typeof(IEsaNamed), Assert.Single(exposed));
    }

    /// <summary>
    /// 声明包含自身时追加实现类型
    /// </summary>
    [Fact]
    public void GetExposedServiceTypes_WhenIncludeSelf_AppendsImplementationType()
    {
        var attribute = new ExposeServicesAttribute(typeof(IEsaNamed)) { IncludeSelf = true };

        var exposed = attribute.GetExposedServiceTypes(typeof(EsaNamed));

        Assert.Contains(typeof(IEsaNamed), exposed);
        Assert.Contains(typeof(EsaNamed), exposed);
    }

    /// <summary>
    /// 声明包含默认服务时追加命名匹配的接口
    /// </summary>
    [Fact]
    public void GetExposedServiceTypes_WhenIncludeDefaults_AppendsNameMatchedInterfaces()
    {
        var attribute = new ExposeServicesAttribute { IncludeDefaults = true };

        var exposed = attribute.GetExposedServiceTypes(typeof(EsaNamed));

        Assert.Equal(typeof(IEsaNamed), Assert.Single(exposed));
    }

    /// <summary>
    /// 同时声明包含默认服务与自身时两者都在
    /// </summary>
    [Fact]
    public void GetExposedServiceTypes_WhenIncludeDefaultsAndSelf_ContainsBoth()
    {
        var attribute = new ExposeServicesAttribute { IncludeDefaults = true, IncludeSelf = true };

        var exposed = attribute.GetExposedServiceTypes(typeof(EsaNamed));

        Assert.Equal(2, exposed.Length);
        Assert.Contains(typeof(IEsaNamed), exposed);
        Assert.Contains(typeof(EsaNamed), exposed);
    }

    /// <summary>
    /// 接口名不是类名后缀时不算默认服务
    /// </summary>
    [Fact]
    public void GetExposedServiceTypes_WhenInterfaceNameDoesNotMatch_IsNotDefaultService()
    {
        var attribute = new ExposeServicesAttribute { IncludeDefaults = true };

        var exposed = attribute.GetExposedServiceTypes(typeof(EsaMismatched));

        Assert.Empty(exposed);
    }

    /// <summary>
    /// 泛型接口按去掉泛型标记的名字参与匹配
    /// </summary>
    [Fact]
    public void GetExposedServiceTypes_WhenGenericInterfaceMatches_IsDefaultService()
    {
        var attribute = new ExposeServicesAttribute { IncludeDefaults = true };

        var exposed = attribute.GetExposedServiceTypes(typeof(EsaGenericHandler));

        Assert.Equal(typeof(IEsaGenericHandler<string>), Assert.Single(exposed));
    }

    /// <summary>
    /// 重复声明的类型只保留一份
    /// </summary>
    [Fact]
    public void GetExposedServiceTypes_WhenDeclaredTypeAlsoDefault_DoesNotDuplicate()
    {
        var attribute = new ExposeServicesAttribute(typeof(IEsaNamed)) { IncludeDefaults = true, IncludeSelf = true };

        var exposed = attribute.GetExposedServiceTypes(typeof(EsaNamed));

        Assert.Equal(2, exposed.Length);
        Assert.Equal(typeof(IEsaNamed), exposed[0]);
    }

    /// <summary>
    /// 构造函数保留声明顺序
    /// </summary>
    [Fact]
    public void ServiceTypes_KeepsDeclarationOrder()
    {
        var attribute = new ExposeServicesAttribute(typeof(IEsaMismatchedContract), typeof(IEsaNamed));

        Assert.Equal(typeof(IEsaMismatchedContract), attribute.ServiceTypes[0]);
        Assert.Equal(typeof(IEsaNamed), attribute.ServiceTypes[1]);
    }

    /// <summary>
    /// 无参构造时声明类型为空且默认不包含任何附加选项
    /// </summary>
    [Fact]
    public void Defaults_AreEmptyTypesAndDisabledFlags()
    {
        var attribute = new ExposeServicesAttribute();

        Assert.Empty(attribute.ServiceTypes);
        Assert.False(attribute.IncludeDefaults);
        Assert.False(attribute.IncludeSelf);
        Assert.Empty(attribute.GetExposedServiceTypes(typeof(EsaNamed)));
    }
}

/// <summary>
/// 名称与实现匹配的契约
/// </summary>
internal interface IEsaNamed;

/// <summary>
/// 名称与实现不匹配的契约
/// </summary>
internal interface IEsaMismatchedContract;

/// <summary>
/// 名称匹配的泛型契约
/// </summary>
/// <typeparam name="T">载荷类型</typeparam>
internal interface IEsaGenericHandler<T>;

/// <summary>
/// 名称与契约匹配的实现
/// </summary>
internal class EsaNamed : IEsaNamed;

/// <summary>
/// 名称与契约不匹配的实现
/// </summary>
internal class EsaMismatched : IEsaMismatchedContract;

/// <summary>
/// 实现泛型契约且名称匹配的实现
/// </summary>
internal class EsaGenericHandler : IEsaGenericHandler<string>;
