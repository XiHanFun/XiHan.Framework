// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.ObjectMapping.Modularity;
using XiHan.Framework.ObjectMapping.Tests.Fakes;

namespace XiHan.Framework.ObjectMapping.Tests;

/// <summary>
/// 扩展属性策略检查器测试
/// </summary>
/// <remarks>
/// 放行矩阵有两个维度：名称列表是否为空、RequiresAll 是 true 还是 false。
/// 其中最反直觉的一格是「RequiresAll = false 且全部不通过」——返回 false 而不是 true，
/// 因为内部用 hasAny 记录是否至少命中一个；名称为空才是无条件放行。
/// 基类三个 Check*Async 默认恒 true，所以矩阵验证必须靠子类替身改写。
/// </remarks>
public class ExtensionPropertyPolicyCheckerTests
{
    /// <summary>
    /// 该类型按约定注册为瞬时依赖
    /// </summary>
    [Fact]
    public void Type_IsRegisteredAsTransientDependency()
    {
        Assert.IsAssignableFrom<ITransientDependency>(new ExtensionPropertyPolicyChecker());
    }

    /// <summary>
    /// 策略为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public async Task CheckPolicyAsync_WhenPolicyNull_ThrowsArgumentNullException()
    {
        var sut = new ExtensionPropertyPolicyChecker();

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => sut.CheckPolicyAsync(null!));

        Assert.Equal("policy", exception.ParamName);
    }

    /// <summary>
    /// 三段策略都为空时无条件放行
    /// </summary>
    [Fact]
    public async Task CheckPolicyAsync_WhenPolicyEmpty_ReturnsTrue()
    {
        var sut = new FakePolicyChecker();

        Assert.True(await sut.CheckPolicyAsync(new ExtensionPropertyPolicyConfiguration()));
        Assert.Empty(sut.CheckedGlobalFeatures);
        Assert.Empty(sut.CheckedFeatures);
        Assert.Empty(sut.CheckedPermissions);
    }

    /// <summary>
    /// 名称数组被置为 null 时同样视为无约束
    /// </summary>
    [Fact]
    public async Task CheckPolicyAsync_WhenNamesNull_TreatsAsNoRequirement()
    {
        var sut = new FakePolicyChecker();
        var policy = new ExtensionPropertyPolicyConfiguration();
        policy.GlobalFeatures.Features = null!;
        policy.Features.Features = null!;
        policy.Permissions.PermissionNames = null!;

        Assert.True(await sut.CheckPolicyAsync(policy));
    }

    /// <summary>
    /// 基类默认实现对任何名称都放行
    /// </summary>
    [Fact]
    public async Task CheckPolicyAsync_WithBaseImplementation_GrantsEveryName()
    {
        var sut = new ExtensionPropertyPolicyChecker();
        var policy = CreatePolicy(
            globalFeatures: ["G1"],
            features: ["F1"],
            permissions: ["P1"]);

        Assert.True(await sut.CheckPolicyAsync(policy));
    }

    /// <summary>
    /// 要求任一命中时，只要有一个通过就放行，且命中后不再继续求值
    /// </summary>
    [Fact]
    public async Task CheckPolicyAsync_WhenRequiresAnyAndFirstGranted_ReturnsTrueAndStopsEarly()
    {
        var sut = new FakePolicyChecker();
        sut.GrantedFeatures.Add("F1");
        var policy = CreatePolicy(features: ["F1", "F2"], featuresRequiresAll: false);

        Assert.True(await sut.CheckPolicyAsync(policy));
        Assert.Equal(new[] { "F1" }, sut.CheckedFeatures.ToArray());
    }

    /// <summary>
    /// 要求任一命中时，前面的不通过会继续往后找
    /// </summary>
    [Fact]
    public async Task CheckPolicyAsync_WhenRequiresAnyAndOnlyLastGranted_ReturnsTrue()
    {
        var sut = new FakePolicyChecker();
        sut.GrantedFeatures.Add("F2");
        var policy = CreatePolicy(features: ["F1", "F2"], featuresRequiresAll: false);

        Assert.True(await sut.CheckPolicyAsync(policy));
        Assert.Equal(new[] { "F1", "F2" }, sut.CheckedFeatures.ToArray());
    }

    /// <summary>
    /// 要求任一命中但一个都没通过时拒绝
    /// </summary>
    [Fact]
    public async Task CheckPolicyAsync_WhenRequiresAnyAndNoneGranted_ReturnsFalse()
    {
        var sut = new FakePolicyChecker();
        var policy = CreatePolicy(features: ["F1", "F2"], featuresRequiresAll: false);

        Assert.False(await sut.CheckPolicyAsync(policy));
        Assert.Equal(new[] { "F1", "F2" }, sut.CheckedFeatures.ToArray());
    }

    /// <summary>
    /// 要求全部命中且全部通过时放行
    /// </summary>
    [Fact]
    public async Task CheckPolicyAsync_WhenRequiresAllAndAllGranted_ReturnsTrue()
    {
        var sut = new FakePolicyChecker();
        sut.GrantedFeatures.Add("F1");
        sut.GrantedFeatures.Add("F2");
        var policy = CreatePolicy(features: ["F1", "F2"], featuresRequiresAll: true);

        Assert.True(await sut.CheckPolicyAsync(policy));
        Assert.Equal(new[] { "F1", "F2" }, sut.CheckedFeatures.ToArray());
    }

    /// <summary>
    /// 要求全部命中时，第一个不通过就立刻拒绝，后面的名称不再求值
    /// </summary>
    [Fact]
    public async Task CheckPolicyAsync_WhenRequiresAllAndFirstDenied_ReturnsFalseAndStopsEarly()
    {
        var sut = new FakePolicyChecker();
        sut.GrantedFeatures.Add("F2");
        var policy = CreatePolicy(features: ["F1", "F2"], featuresRequiresAll: true);

        Assert.False(await sut.CheckPolicyAsync(policy));
        Assert.Equal(new[] { "F1" }, sut.CheckedFeatures.ToArray());
    }

    /// <summary>
    /// 全局功能不通过时直接拒绝，功能与权限完全不再检查
    /// </summary>
    [Fact]
    public async Task CheckPolicyAsync_WhenGlobalFeatureDenied_SkipsFeatureAndPermissionChecks()
    {
        var sut = new FakePolicyChecker();
        sut.GrantedFeatures.Add("F1");
        sut.GrantedPermissions.Add("P1");
        var policy = CreatePolicy(
            globalFeatures: ["G1"],
            features: ["F1"],
            permissions: ["P1"]);

        Assert.False(await sut.CheckPolicyAsync(policy));
        Assert.Equal(new[] { "G1" }, sut.CheckedGlobalFeatures.ToArray());
        Assert.Empty(sut.CheckedFeatures);
        Assert.Empty(sut.CheckedPermissions);
    }

    /// <summary>
    /// 功能不通过时直接拒绝，权限不再检查
    /// </summary>
    [Fact]
    public async Task CheckPolicyAsync_WhenFeatureDenied_SkipsPermissionChecks()
    {
        var sut = new FakePolicyChecker();
        sut.GrantedGlobalFeatures.Add("G1");
        sut.GrantedPermissions.Add("P1");
        var policy = CreatePolicy(
            globalFeatures: ["G1"],
            features: ["F1"],
            permissions: ["P1"]);

        Assert.False(await sut.CheckPolicyAsync(policy));
        Assert.Equal(new[] { "G1" }, sut.CheckedGlobalFeatures.ToArray());
        Assert.Equal(new[] { "F1" }, sut.CheckedFeatures.ToArray());
        Assert.Empty(sut.CheckedPermissions);
    }

    /// <summary>
    /// 权限不通过时整体拒绝
    /// </summary>
    [Fact]
    public async Task CheckPolicyAsync_WhenPermissionDenied_ReturnsFalse()
    {
        var sut = new FakePolicyChecker();
        sut.GrantedGlobalFeatures.Add("G1");
        sut.GrantedFeatures.Add("F1");
        var policy = CreatePolicy(
            globalFeatures: ["G1"],
            features: ["F1"],
            permissions: ["P1"]);

        Assert.False(await sut.CheckPolicyAsync(policy));
        Assert.Equal(new[] { "P1" }, sut.CheckedPermissions.ToArray());
    }

    /// <summary>
    /// 三段全部通过时放行，且检查顺序为全局功能、功能、权限
    /// </summary>
    [Fact]
    public async Task CheckPolicyAsync_WhenAllSectionsGranted_ReturnsTrue()
    {
        var sut = new FakePolicyChecker();
        sut.GrantedGlobalFeatures.Add("G1");
        sut.GrantedFeatures.Add("F1");
        sut.GrantedPermissions.Add("P1");
        var policy = CreatePolicy(
            globalFeatures: ["G1"],
            features: ["F1"],
            permissions: ["P1"]);

        Assert.True(await sut.CheckPolicyAsync(policy));
        Assert.Equal(new[] { "G1" }, sut.CheckedGlobalFeatures.ToArray());
        Assert.Equal(new[] { "F1" }, sut.CheckedFeatures.ToArray());
        Assert.Equal(new[] { "P1" }, sut.CheckedPermissions.ToArray());
    }

    /// <summary>
    /// 只约束权限时，全局功能与功能两段被跳过
    /// </summary>
    [Fact]
    public async Task CheckPolicyAsync_WhenOnlyPermissionConfigured_ChecksPermissionOnly()
    {
        var sut = new FakePolicyChecker();
        sut.GrantedPermissions.Add("P1");
        var policy = CreatePolicy(permissions: ["P1"]);

        Assert.True(await sut.CheckPolicyAsync(policy));
        Assert.Empty(sut.CheckedGlobalFeatures);
        Assert.Empty(sut.CheckedFeatures);
        Assert.Equal(new[] { "P1" }, sut.CheckedPermissions.ToArray());
    }

    /// <summary>
    /// 构造一份指定了三段约束的策略配置
    /// </summary>
    /// <param name="globalFeatures">全局功能名称</param>
    /// <param name="globalRequiresAll">全局功能是否要求全部命中</param>
    /// <param name="features">功能名称</param>
    /// <param name="featuresRequiresAll">功能是否要求全部命中</param>
    /// <param name="permissions">权限名称</param>
    /// <param name="permissionsRequiresAll">权限是否要求全部命中</param>
    /// <returns>策略配置</returns>
    private static ExtensionPropertyPolicyConfiguration CreatePolicy(
        string[]? globalFeatures = null,
        bool globalRequiresAll = false,
        string[]? features = null,
        bool featuresRequiresAll = false,
        string[]? permissions = null,
        bool permissionsRequiresAll = false)
    {
        var policy = new ExtensionPropertyPolicyConfiguration();

        policy.GlobalFeatures.Features = globalFeatures ?? [];
        policy.GlobalFeatures.RequiresAll = globalRequiresAll;
        policy.Features.Features = features ?? [];
        policy.Features.RequiresAll = featuresRequiresAll;
        policy.Permissions.PermissionNames = permissions ?? [];
        policy.Permissions.RequiresAll = permissionsRequiresAll;

        return policy;
    }
}
