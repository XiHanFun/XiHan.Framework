// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.ObjectMapping.Tests.Fakes;

/// <summary>
/// 可编程放行结果的扩展属性策略检查器替身
/// </summary>
/// <remarks>
/// 基类三个 Check*Async 默认恒为 true，无法区分放行矩阵，因此这里改写为「白名单命中才放行」，
/// 同时按调用顺序记录被检查过的名称，用于断言短路行为（哪些名称压根没被求值）。
/// </remarks>
public class FakePolicyChecker : ExtensionPropertyPolicyChecker
{
    /// <summary>
    /// 允许通过的全局功能白名单
    /// </summary>
    public HashSet<string> GrantedGlobalFeatures { get; } = [];

    /// <summary>
    /// 允许通过的功能白名单
    /// </summary>
    public HashSet<string> GrantedFeatures { get; } = [];

    /// <summary>
    /// 允许通过的权限白名单
    /// </summary>
    public HashSet<string> GrantedPermissions { get; } = [];

    /// <summary>
    /// 按调用顺序记录被检查的全局功能名
    /// </summary>
    public List<string> CheckedGlobalFeatures { get; } = [];

    /// <summary>
    /// 按调用顺序记录被检查的功能名
    /// </summary>
    public List<string> CheckedFeatures { get; } = [];

    /// <summary>
    /// 按调用顺序记录被检查的权限名
    /// </summary>
    public List<string> CheckedPermissions { get; } = [];

    /// <summary>
    /// 全局功能检查
    /// </summary>
    /// <param name="featureName">全局功能名称</param>
    /// <returns>是否放行</returns>
    protected override Task<bool> CheckGlobalFeaturesAsync(string featureName)
    {
        CheckedGlobalFeatures.Add(featureName);
        return Task.FromResult(GrantedGlobalFeatures.Contains(featureName));
    }

    /// <summary>
    /// 功能检查
    /// </summary>
    /// <param name="featureName">功能名称</param>
    /// <returns>是否放行</returns>
    protected override Task<bool> CheckFeaturesAsync(string featureName)
    {
        CheckedFeatures.Add(featureName);
        return Task.FromResult(GrantedFeatures.Contains(featureName));
    }

    /// <summary>
    /// 权限检查
    /// </summary>
    /// <param name="permissionName">权限名称</param>
    /// <returns>是否放行</returns>
    protected override Task<bool> CheckPermissionsAsync(string permissionName)
    {
        CheckedPermissions.Add(permissionName);
        return Task.FromResult(GrantedPermissions.Contains(permissionName));
    }
}
