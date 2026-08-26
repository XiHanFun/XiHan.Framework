// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Application.Attributes;
using XiHan.Framework.Application.Contracts.Services;
using XiHan.Framework.Caching.Attributes;

namespace XiHan.Framework.Web.Api.Tests.DynamicApiGeneration;

/// <summary>
/// 带缓存标注的应用服务
/// </summary>
[DynamicApi(Name = "cacheable-sample")]
public class CacheableAppService : IApplicationService
{
    /// <summary>
    /// 查询档案
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <returns>档案内容</returns>
    [Cacheable(Key = "profile:{userId}", ExpireSeconds = 60)]
    public Task<string> GetProfileAsync(string userId)
    {
        return Task.FromResult(userId);
    }

    /// <summary>
    /// 更新档案
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <returns>档案内容</returns>
    [CacheEvict(Key = "profile:{userId}")]
    public Task<string> UpdateProfileAsync(string userId)
    {
        return Task.FromResult(userId);
    }

    /// <summary>
    /// 探活
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <returns>档案内容</returns>
    public Task<string> PingAsync(string userId)
    {
        return Task.FromResult(userId);
    }
}
