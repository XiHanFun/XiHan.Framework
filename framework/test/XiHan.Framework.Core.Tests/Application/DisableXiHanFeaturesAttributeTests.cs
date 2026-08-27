// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Application;

namespace XiHan.Framework.Core.Tests.Application;

/// <summary>
/// 禁用曦寒功能特性测试
/// </summary>
/// <remarks>
/// 这个特性一旦标注即"全部关掉"，三个开关的默认值都是 true——
/// 语义是"标了就默认全禁"，改成 false 会让标注了特性的类型悄悄恢复被拦截，因此逐个锁死。
/// </remarks>
public class DisableXiHanFeaturesAttributeTests
{
    /// <summary>
    /// 三个开关默认全部为真，标注即全禁
    /// </summary>
    [Fact]
    public void Constructor_Defaults_DisableEverything()
    {
        var attribute = new DisableXiHanFeaturesAttribute();

        Assert.True(attribute.DisableInterceptors);
        Assert.True(attribute.DisableMiddleware);
        Assert.True(attribute.DisableMvcFilters);
    }

    /// <summary>
    /// 三个开关都可以在标注时单独放开
    /// </summary>
    [Fact]
    public void Properties_CanBeRelaxedIndividually()
    {
        var attribute = new DisableXiHanFeaturesAttribute
        {
            DisableInterceptors = false,
            DisableMiddleware = false,
            DisableMvcFilters = false
        };

        Assert.False(attribute.DisableInterceptors);
        Assert.False(attribute.DisableMiddleware);
        Assert.False(attribute.DisableMvcFilters);
    }

    /// <summary>
    /// 特性只能标注在类上，且默认可被派生类继承、不允许重复标注
    /// </summary>
    [Fact]
    public void AttributeUsage_TargetsClassOnly()
    {
        var usage = typeof(DisableXiHanFeaturesAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.Equal(AttributeTargets.Class, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
        Assert.True(usage.Inherited);
    }

    /// <summary>
    /// 标注在类型上后可以按标准反射读回，且默认值随之带出
    /// </summary>
    [Fact]
    public void Attribute_IsReadableFromDecoratedType()
    {
        var attribute = typeof(FeatureDisabledSample)
            .GetCustomAttributes(typeof(DisableXiHanFeaturesAttribute), false)
            .Cast<DisableXiHanFeaturesAttribute>()
            .Single();

        Assert.True(attribute.DisableInterceptors);
        Assert.False(attribute.DisableMiddleware);
    }
}

/// <summary>
/// 标注了禁用曦寒功能特性的样例类型
/// </summary>
[DisableXiHanFeatures(DisableMiddleware = false)]
public class FeatureDisabledSample
{
}
