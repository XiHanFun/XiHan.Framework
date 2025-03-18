#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TransientCachedServiceProvider
// Guid:3dca0b1c-0099-4d9b-829f-96d1c9a019ba
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/28 2:52:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 瞬时缓存服务提供器
/// </summary>
[ExposeServices(typeof(ITransientCachedServiceProvider))]
public class TransientCachedServiceProvider : CachedServiceProviderBase, ITransientCachedServiceProvider, ITransientDependency
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceProvider"></param>
    public TransientCachedServiceProvider(IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
    }
}
