#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ApplicationInitializationContext
// Guid:2d22c3b8-23c6-439c-8cdf-1b8de2edcbd3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 19:39:44
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Core.Application;

/// <summary>
/// 应用初始化上下文
/// </summary>
public class ApplicationInitializationContext : IServiceProviderAccessor
{
    /// <summary>
    /// 服务提供者
    /// </summary>
    public IServiceProvider ServiceProvider { get; set; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceProvider"></param>
    public ApplicationInitializationContext([NotNull] IServiceProvider serviceProvider)
    {
        CheckHelper.NotNull(serviceProvider, nameof(serviceProvider));

        ServiceProvider = serviceProvider;
    }
}