#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ServiceProviderKeyedServiceExtensions
// Guid:7c6fc2b2-f106-47e0-bdb9-0b829b768adf
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/27 1:54:02
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Core.Extensions.DependencyInjection;

/// <summary>
/// 服务提供者键控服务扩展方法
/// </summary>
public static class ServiceProviderKeyedServiceExtensions
{
    /// <summary>
    /// 获取键控服务
    /// </summary>
    /// <param name="provider"></param>
    /// <param name="serviceType"></param>
    /// <param name="serviceKey"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static object? GetKeyedService(this IServiceProvider provider, Type serviceType, object? serviceKey)
    {
        CheckHelper.NotNull(provider, nameof(provider));

        if (provider is IKeyedServiceProvider keyedServiceProvider)
        {
            return keyedServiceProvider.GetKeyedService(serviceType, serviceKey);
        }

        throw new InvalidOperationException("这个服务提供者不支持键控服务。 ");
    }
}