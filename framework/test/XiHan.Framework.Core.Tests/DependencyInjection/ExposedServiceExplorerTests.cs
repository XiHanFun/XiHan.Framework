// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.DependencyInjection;

namespace XiHan.Framework.Core.Tests.DependencyInjection;

/// <summary>
/// 暴露服务探测器测试
/// </summary>
/// <remarks>
/// 探测器负责把类型上的各种暴露声明汇总成最终清单，三条分支必须锁死：
/// 无任何声明时回落到「默认服务 + 自身」；只声明键值服务时不再暴露默认服务；
/// 多个暴露特性的结果合并去重。
/// </remarks>
public class ExposedServiceExplorerTests
{
    /// <summary>
    /// 无暴露声明时回落到默认服务与自身
    /// </summary>
    [Fact]
    public void GetExposedServices_WhenNoAttribute_ReturnsDefaultsAndSelf()
    {
        var exposed = ExposedServiceExplorer.GetExposedServices(typeof(EseBareService));

        Assert.Contains(typeof(IEseBareService), exposed);
        Assert.Contains(typeof(EseBareService), exposed);
    }

    /// <summary>
    /// 存在暴露特性时以特性声明为准
    /// </summary>
    [Fact]
    public void GetExposedServices_WhenAttributePresent_UsesAttributeResult()
    {
        var exposed = ExposedServiceExplorer.GetExposedServices(typeof(EseExplicitService));

        Assert.Equal(typeof(IEseExplicitContract), Assert.Single(exposed));
    }

    /// <summary>
    /// 多个暴露特性的结果合并且去重
    /// </summary>
    [Fact]
    public void GetExposedServices_WhenMultipleAttributes_MergesAndDeduplicates()
    {
        var exposed = ExposedServiceExplorer.GetExposedServices(typeof(EseMultiAttributeService));

        Assert.Equal(2, exposed.Count);
        Assert.Contains(typeof(IEseExplicitContract), exposed);
        Assert.Contains(typeof(IEseSecondContract), exposed);
    }

    /// <summary>
    /// 只有键值暴露声明时不再暴露默认服务
    /// </summary>
    [Fact]
    public void GetExposedServices_WhenOnlyKeyedAttribute_ReturnsEmpty()
    {
        Assert.Empty(ExposedServiceExplorer.GetExposedServices(typeof(EseKeyedOnlyService)));
    }

    /// <summary>
    /// 键值与普通暴露声明并存时普通声明照常生效
    /// </summary>
    [Fact]
    public void GetExposedServices_WhenKeyedAndPlainAttribute_KeepsPlainDeclaration()
    {
        var exposed = ExposedServiceExplorer.GetExposedServices(typeof(EseKeyedAndPlainService));

        Assert.Equal(typeof(IEseExplicitContract), Assert.Single(exposed));
    }

    /// <summary>
    /// 无键值暴露声明时键值清单为空
    /// </summary>
    [Fact]
    public void GetExposedKeyedServices_WhenNoKeyedAttribute_ReturnsEmpty()
    {
        Assert.Empty(ExposedServiceExplorer.GetExposedKeyedServices(typeof(EseBareService)));
    }

    /// <summary>
    /// 键值暴露声明产出带键的服务标识
    /// </summary>
    [Fact]
    public void GetExposedKeyedServices_WhenKeyedAttribute_ReturnsIdentifierWithKey()
    {
        var identifier = Assert.Single(ExposedServiceExplorer.GetExposedKeyedServices(typeof(EseKeyedOnlyService)));

        Assert.Equal(typeof(IEseKeyedContract), identifier.ServiceType);
        Assert.Equal("ese", identifier.ServiceKey);
    }

    /// <summary>
    /// 多个键值暴露声明按声明顺序全部保留
    /// </summary>
    [Fact]
    public void GetExposedKeyedServices_WhenMultipleKeys_KeepsEveryIdentifier()
    {
        var identifiers = ExposedServiceExplorer.GetExposedKeyedServices(typeof(EseMultiKeyedService));

        Assert.Equal(2, identifiers.Count);
        Assert.Contains(identifiers, i => Equals(i.ServiceKey, "first"));
        Assert.Contains(identifiers, i => Equals(i.ServiceKey, "second"));
    }
}

/// <summary>
/// 无暴露声明的样例契约
/// </summary>
internal interface IEseBareService;

/// <summary>
/// 显式暴露的样例契约
/// </summary>
internal interface IEseExplicitContract;

/// <summary>
/// 第二个显式暴露的样例契约
/// </summary>
internal interface IEseSecondContract;

/// <summary>
/// 键值暴露的样例契约
/// </summary>
internal interface IEseKeyedContract;

/// <summary>
/// 无暴露声明的样例服务
/// </summary>
internal class EseBareService : IEseBareService;

/// <summary>
/// 显式暴露的样例服务
/// </summary>
[ExposeServices(typeof(IEseExplicitContract))]
internal class EseExplicitService : IEseExplicitContract;

/// <summary>
/// 多个暴露特性的样例服务
/// </summary>
[ExposeServices(typeof(IEseExplicitContract))]
[ExposeServices(typeof(IEseExplicitContract), typeof(IEseSecondContract))]
internal class EseMultiAttributeService : IEseExplicitContract, IEseSecondContract;

/// <summary>
/// 只声明键值暴露的样例服务
/// </summary>
[ExposeKeyedServiceAttribute<IEseKeyedContract>("ese")]
internal class EseKeyedOnlyService : IEseKeyedContract;

/// <summary>
/// 键值与普通暴露并存的样例服务
/// </summary>
[ExposeServices(typeof(IEseExplicitContract))]
[ExposeKeyedServiceAttribute<IEseKeyedContract>("ese")]
internal class EseKeyedAndPlainService : IEseExplicitContract, IEseKeyedContract;

/// <summary>
/// 多个键值暴露的样例服务
/// </summary>
[ExposeKeyedServiceAttribute<IEseKeyedContract>("first")]
[ExposeKeyedServiceAttribute<IEseKeyedContract>("second")]
internal class EseMultiKeyedService : IEseKeyedContract;
