#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultDistributedCacheKeyNormalizer
// Guid:cb8b3556-0df2-4727-a11c-a67033d72bf4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Caching;

/// <summary>
/// 默认分布式缓存键规范化器
/// </summary>
public class DefaultDistributedCacheKeyNormalizer(IServiceProvider serviceProvider) : IDistributedCacheKeyNormalizer
{
    /// <summary>
    /// 规范化缓存键
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public string NormalizeKey(DistributedCacheKeyNormalizeArgs args)
    {
        var tenantSegment = "host";
        if (!args.IgnoreMultiTenancy)
        {
            var currentTenantAccessor = serviceProvider.GetService<ICurrentTenantAccessor>();
            var currentTenant = currentTenantAccessor?.Current;
            if (currentTenant?.TenantId is not null)
            {
                tenantSegment = currentTenant.Name.IsNullOrWhiteSpace()
                    ? currentTenant.TenantId.Value.ToString("N")
                    : currentTenant.Name!;
            }
        }

        return $"{tenantSegment}:{args.CacheName}:{args.Key}";
    }
}
