#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IXiHanApplicationWithExternalServiceProvider
// Guid:7fb05619-bbbe-49a5-8943-e717e7fdb4fa
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/28 2:48:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics.CodeAnalysis;

namespace XiHan.Framework.Core.Application;

/// <summary>
/// 具有外部服务的曦寒应用提供器接口
/// </summary>
public interface IXiHanApplicationWithExternalServiceProvider : IXiHanApplication
{
    /// <summary>
    /// 设置服务提供器，但不初始化模块
    /// </summary>
    void SetServiceProvider([NotNull] IServiceProvider serviceProvider);

    /// <summary>
    /// 设置服务提供器并初始化所有模块，异步
    /// 如果之前调用过 <see cref="SetServiceProvider"/>，则应将相同的 <paramref name="serviceProvider"/> 实例传递给此方法
    /// </summary>
    Task InitializeAsync([NotNull] IServiceProvider serviceProvider);

    /// <summary>
    /// 设置服务提供器并初始化所有模块
    /// 如果之前调用过 <see cref="SetServiceProvider"/>，则应将相同的 <paramref name="serviceProvider"/> 实例传递给此方法
    /// </summary>
    void Initialize([NotNull] IServiceProvider serviceProvider);
}