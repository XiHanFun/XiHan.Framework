#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanCachingModule
// Guid:6f62e6f6-d4e0-46a0-a7cc-cdbb69489ba9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 3:29:38
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Caching;

/// <summary>
/// 曦寒框架缓存模块
/// </summary>
public class XiHanCachingModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        _ = services.AddMemoryCache();
        _ = services.AddDistributedMemoryCache();

        _ = services.AddSingleton(typeof(IDistributedCache<>), typeof(DistributedCache<>));
        _ = services.AddSingleton(typeof(IDistributedCache<,>), typeof(DistributedCache<,>));
    }
}
