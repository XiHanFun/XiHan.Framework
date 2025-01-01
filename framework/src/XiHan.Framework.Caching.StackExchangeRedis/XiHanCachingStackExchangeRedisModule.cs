#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanCachingStackExchangeRedisModule
// Guid:92256627-ddc6-464e-aa27-854d122be589
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/13 4:39:22
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Utils.Text;

namespace XiHan.Framework.Caching.StackExchangeRedis;

/// <summary>
/// XiHanCachingStackExchangeRedisModule
/// </summary>
[DependsOn(
    typeof(XiHanCachingModule)
)]
public class XiHanCachingStackExchangeRedisModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();

        var redisEnabled = configuration["Redis:IsEnabled"];
        if (!string.IsNullOrEmpty(redisEnabled) && !bool.Parse(redisEnabled))
        {
            return;
        }

        _ = context.Services.AddStackExchangeRedisCache(options =>
        {
            var redisConfiguration = configuration["Redis:Configuration"];
            if (!redisConfiguration.IsNullOrEmpty())
            {
                options.Configuration = redisConfiguration;
            }
        });

        _ = context.Services.Replace(ServiceDescriptor.Singleton<IDistributedCache, XiHanRedisCache>());
    }
}
