#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ICachedServiceProviderBase
// Guid:383d416c-9a20-4ee3-bbde-54b448773647
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/27 2:16:52
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 缓存服务提供程序基类接口
/// </summary>
public interface ICachedServiceProviderBase : IKeyedServiceProvider
{
    /// <summary>
    /// 获取服务
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    T GetService<T>(T defaultValue);

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <param name="serviceType"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    object GetService(Type serviceType, object defaultValue);

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="factory"></param>
    /// <returns></returns>
    T GetService<T>(Func<IServiceProvider, object> factory);

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <param name="serviceType"></param>
    /// <param name="factory"></param>
    /// <returns></returns>
    object GetService(Type serviceType, Func<IServiceProvider, object> factory);
}
