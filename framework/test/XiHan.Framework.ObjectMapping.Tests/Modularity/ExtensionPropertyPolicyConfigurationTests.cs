// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.ObjectMapping.Modularity;

namespace XiHan.Framework.ObjectMapping.Tests.Modularity;

/// <summary>
/// 扩展属性策略配置测试
/// </summary>
/// <remarks>
/// 构造函数必须把三段子配置都建好：策略检查器直接读 policy.GlobalFeatures.Features 等成员，
/// 任何一段为 null 都会在检查阶段炸空引用，所以「非空」本身就是契约的一部分。
/// </remarks>
public class ExtensionPropertyPolicyConfigurationTests
{
    /// <summary>
    /// 构造后三段子配置都已就绪且互为独立实例
    /// </summary>
    [Fact]
    public void Constructor_CreatesAllThreeSections()
    {
        var sut = new ExtensionPropertyPolicyConfiguration();

        Assert.NotNull(sut.GlobalFeatures);
        Assert.NotNull(sut.Features);
        Assert.NotNull(sut.Permissions);
    }

    /// <summary>
    /// 默认状态下三段都不带任何约束，等价于「无策略」
    /// </summary>
    [Fact]
    public void Constructor_LeavesEverySectionWithoutRequirement()
    {
        var sut = new ExtensionPropertyPolicyConfiguration();

        Assert.Empty(sut.GlobalFeatures.Features);
        Assert.Empty(sut.Features.Features);
        Assert.Empty(sut.Permissions.PermissionNames);
        Assert.False(sut.GlobalFeatures.RequiresAll);
        Assert.False(sut.Features.RequiresAll);
        Assert.False(sut.Permissions.RequiresAll);
    }

    /// <summary>
    /// 两个实例之间不共享子配置对象
    /// </summary>
    [Fact]
    public void Constructor_DoesNotShareSectionsBetweenInstances()
    {
        var first = new ExtensionPropertyPolicyConfiguration();
        var second = new ExtensionPropertyPolicyConfiguration();

        Assert.NotSame(first.GlobalFeatures, second.GlobalFeatures);
        Assert.NotSame(first.Features, second.Features);
        Assert.NotSame(first.Permissions, second.Permissions);
    }

    /// <summary>
    /// 三段子配置均可整体替换
    /// </summary>
    [Fact]
    public void Sections_CanBeReplaced()
    {
        var sut = new ExtensionPropertyPolicyConfiguration();
        var globalFeatures = new ExtensionPropertyGlobalFeaturePolicyConfiguration();
        var features = new ExtensionPropertyFeaturePolicyConfiguration();
        var permissions = new ExtensionPropertyPermissionPolicyConfiguration();

        sut.GlobalFeatures = globalFeatures;
        sut.Features = features;
        sut.Permissions = permissions;

        Assert.Same(globalFeatures, sut.GlobalFeatures);
        Assert.Same(features, sut.Features);
        Assert.Same(permissions, sut.Permissions);
    }
}
