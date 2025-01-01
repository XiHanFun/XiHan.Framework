#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DisableXiHanFeaturesAttribute
// Guid:25b8300b-7cff-43ed-a0f6-8c1ff6038cf0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/28 3:39:51
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.Application;

/// <summary>
/// 禁用曦寒功能特性
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class DisableXiHanFeaturesAttribute : Attribute
{
    /// <summary>
    /// 将不会为该类注册任何拦截器
    /// 这将导致所有依赖拦截器的功能无法工作
    /// </summary>
    public bool DisableInterceptors { get; set; } = true;

    /// <summary>
    /// 中间件将跳过该类
    /// 这将导致所有依赖中间件的功能无法工作
    /// </summary>
    public bool DisableMiddleware { get; set; } = true;

    /// <summary>
    /// 不会为该类移除所有内置过滤器
    /// 这将导致所有依赖过滤器的功能无法工作
    /// </summary>
    public bool DisableMvcFilters { get; set; } = true;
}
