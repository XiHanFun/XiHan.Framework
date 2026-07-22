// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Caching.Distributed.Abstracts;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Caching.Distributed;

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
        var tenantSegment = "0";
        if (!args.IgnoreMultiTenancy)
        {
            var currentTenantAccessor = serviceProvider.GetService<ICurrentTenantAccessor>();
            var currentTenant = currentTenantAccessor?.Current;
            if (currentTenant?.TenantId is not null)
            {
                // 租户段必须使用稳定且不可变的 TenantId：
                tenantSegment = currentTenant.TenantId.Value.ToString();
            }
        }

        return $"{tenantSegment}:{args.CacheName}:{args.Key}";
    }
}
