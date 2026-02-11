#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CachedServiceProvider
// Guid:f0108928-2ba4-472e-9b49-46903141f69b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/04/27 21:45:47
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 已缓存服务提供器
/// </summary>
[ExposeServices(typeof(ICachedServiceProvider))]
public class CachedServiceProvider : CachedServiceProviderBase, ICachedServiceProvider, IScopedDependency
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceProvider"></param>
    public CachedServiceProvider(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }
}
