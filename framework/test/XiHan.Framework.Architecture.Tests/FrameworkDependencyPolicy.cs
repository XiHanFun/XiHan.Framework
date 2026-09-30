// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Architecture.Tests.ProjectGraph;

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 本仓库的依赖例外清单
/// </summary>
internal static class FrameworkDependencyPolicy
{
    /// <summary>
    /// 当前允许的向上引用
    /// </summary>
    public static DependencyPolicy Current { get; } = new(
    [
        new DependencyException("XiHan.Framework.Application", "XiHan.Framework.DistributedIds", "应用模块经 [DependsOn] 装配分布式 ID 模块"),
        new DependencyException("XiHan.Framework.Application", "XiHan.Framework.Logging", "应用模块经 [DependsOn] 装配日志模块"),
        new DependencyException("XiHan.Framework.Application", "XiHan.Framework.ObjectMapping", "应用模块经 [DependsOn] 装配对象映射模块"),
        new DependencyException("XiHan.Framework.MultiTenancy", "XiHan.Framework.Security", "按当前用户解析租户使用 Security.Users"),
        new DependencyException("XiHan.Framework.Settings", "XiHan.Framework.Security", "按用户读取设置值使用 Security.Users")
    ]);
}
