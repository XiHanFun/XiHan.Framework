// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Application;

namespace XiHan.Framework.Core.Tests.Application;

/// <summary>
/// 集成服务特性测试
/// </summary>
/// <remarks>
/// 判定方法有两条独立分支：类型自身（含基类继承）与所实现的接口。
/// 「实现了被标注的接口也算集成服务」是这个特性区别于普通反射判定的关键行为，单独立用例。
/// </remarks>
public class IntegrationServiceAttributeTests
{
    /// <summary>
    /// 类型自身标注时判定为集成服务
    /// </summary>
    [Fact]
    public void IsDefinedOrInherited_WhenTypeIsDecorated_ReturnsTrue()
    {
        Assert.True(IntegrationServiceAttribute.IsDefinedOrInherited(typeof(DecoratedIntegrationSample)));
        Assert.True(IntegrationServiceAttribute.IsDefinedOrInherited<DecoratedIntegrationSample>());
    }

    /// <summary>
    /// 基类标注时派生类同样判定为集成服务
    /// </summary>
    [Fact]
    public void IsDefinedOrInherited_WhenBaseTypeIsDecorated_ReturnsTrue()
    {
        Assert.True(IntegrationServiceAttribute.IsDefinedOrInherited(typeof(DerivedFromDecoratedIntegrationSample)));
    }

    /// <summary>
    /// 实现了被标注的接口时判定为集成服务
    /// </summary>
    /// <remarks>
    /// 这一条不能靠 <c>Type.IsDefined(inherit: true)</c> 得到——接口上的特性不会沿实现关系继承，
    /// 必须由特性自己遍历 <c>GetInterfaces()</c>，因此单独锁死。
    /// </remarks>
    [Fact]
    public void IsDefinedOrInherited_WhenInterfaceIsDecorated_ReturnsTrue()
    {
        Assert.False(typeof(ImplementsDecoratedIntegrationContract).IsDefined(typeof(IntegrationServiceAttribute), true));
        Assert.True(IntegrationServiceAttribute.IsDefinedOrInherited(typeof(ImplementsDecoratedIntegrationContract)));
    }

    /// <summary>
    /// 既未标注也未实现被标注接口时判定为否
    /// </summary>
    [Fact]
    public void IsDefinedOrInherited_WhenNothingIsDecorated_ReturnsFalse()
    {
        Assert.False(IntegrationServiceAttribute.IsDefinedOrInherited(typeof(PlainIntegrationSample)));
        Assert.False(IntegrationServiceAttribute.IsDefinedOrInherited<PlainIntegrationSample>());
    }

    /// <summary>
    /// 特性可标注在类与接口上，且可被继承、不允许重复标注
    /// </summary>
    [Fact]
    public void AttributeUsage_TargetsClassAndInterface()
    {
        var usage = typeof(IntegrationServiceAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.Equal(AttributeTargets.Class | AttributeTargets.Interface, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
        Assert.True(usage.Inherited);
    }
}

/// <summary>
/// 被标注的集成服务契约
/// </summary>
[IntegrationService]
public interface IDecoratedIntegrationContract
{
}

/// <summary>
/// 类型自身被标注的样例
/// </summary>
[IntegrationService]
public class DecoratedIntegrationSample
{
}

/// <summary>
/// 继承自被标注类型的样例
/// </summary>
public class DerivedFromDecoratedIntegrationSample : DecoratedIntegrationSample
{
}

/// <summary>
/// 实现了被标注契约的样例
/// </summary>
public class ImplementsDecoratedIntegrationContract : IDecoratedIntegrationContract
{
}

/// <summary>
/// 完全未标注的样例
/// </summary>
public class PlainIntegrationSample
{
}
