#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanDistributedIdsServiceCollectionExtensions
// Guid:d0e5f6a7-9b8c-4d0e-b7f4-5a6b9c8d0e2f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 11:10:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.DistributedIds.Guids;
using XiHan.Framework.DistributedIds.NanoIds;
using XiHan.Framework.DistributedIds.SnowflakeIds;
using XiHan.Framework.DistributedIds.Sqids;

namespace XiHan.Framework.DistributedIds.Extensions.DependencyInjection;

/// <summary>
/// 服务集合扩展方法
/// </summary>
public static class XiHanDistributedIdsServiceCollectionExtensions
{
    /// <summary>
    /// 添加 XiHan 日志服务
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configuration"></param>
    /// <returns></returns>
    public static IServiceCollection AddXiHanDistributedIds(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SnowflakeIdOptions>(configuration.GetSection(SnowflakeIdOptions.SectionName));
        services.Configure<SequentialGuidOptions>(configuration.GetSection(SequentialGuidOptions.SectionName));
        services.Configure<NanoIdOptions>(configuration.GetSection(NanoIdOptions.SectionName));
        services.Configure<SqidsOptions>(configuration.GetSection(SqidsOptions.SectionName));

        var guidGenerator = IdGeneratorFactory.CreateSequentialGuidGenerator_Default();
        services.AddSingleton<IDistributedIdGenerator<Guid>>(guidGenerator);

        var longGenerator = IdGeneratorFactory.CreateSnowflakeIdGenerator_HighWorkload();
        services.AddSingleton<IDistributedIdGenerator<long>>(longGenerator);

        return services;
    }
}
