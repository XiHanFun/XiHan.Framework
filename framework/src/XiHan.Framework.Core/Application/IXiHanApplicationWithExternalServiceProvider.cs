// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.Application;

/// <summary>
/// 具有外部服务的曦寒应用提供器接口
/// </summary>
public interface IXiHanApplicationWithExternalServiceProvider : IXiHanApplication
{
    /// <summary>
    /// 设置服务提供器，但不初始化模块
    /// </summary>
    void SetServiceProvider(IServiceProvider serviceProvider);

    /// <summary>
    /// 设置服务提供器并初始化所有模块，异步
    /// 如果之前调用过 <see cref="SetServiceProvider"/>，则应将相同的 <paramref name="serviceProvider"/> 实例传递给此方法
    /// </summary>
    Task InitializeAsync(IServiceProvider serviceProvider);

    /// <summary>
    /// 设置服务提供器并初始化所有模块
    /// 如果之前调用过 <see cref="SetServiceProvider"/>，则应将相同的 <paramref name="serviceProvider"/> 实例传递给此方法
    /// </summary>
    void Initialize(IServiceProvider serviceProvider);
}
