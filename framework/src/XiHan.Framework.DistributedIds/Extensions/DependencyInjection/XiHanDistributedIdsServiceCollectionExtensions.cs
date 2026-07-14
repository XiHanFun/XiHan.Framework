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
using Microsoft.Extensions.Options;
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
    /// 添加 XiHan 分布式 ID 服务
    /// </summary>
    /// <remarks>
    /// 生成器由「已绑定的配置」构建，因此 <c>XiHan:DistributedIds:SnowflakeId</c> 下的
    /// WorkerId / DataCenterId / BaseTime / 位长等键真正生效。
    /// <para>
    /// 雪花 ID 采用「基线默认值 + 配置覆盖」：先落高负载基线（WorkerIdBitLength=6、SeqBitLength=12），
    /// 再用配置节覆盖——配置未写的键保持基线（与历史默认行为一致，不改变既有 ID 的位布局），
    /// 写了的键则真正生效。
    /// </para>
    /// </remarks>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合，支持链式调用</returns>
    public static IServiceCollection AddXiHanDistributedIds(this IServiceCollection services, IConfiguration configuration)
    {
        // 基线默认值（等价于原先硬编码的 HighWorkload 预设）→ 再由配置节覆盖。
        // 顺序不可颠倒：Configure 先注册、Bind 后注册，后者才能覆盖前者。
        services.AddOptions<SnowflakeIdOptions>()
            .Configure(options =>
            {
                options.WorkerIdBitLength = 6;
                options.SeqBitLength = 12;
                options.WorkerId = 1;
            })
            .Bind(configuration.GetSection(SnowflakeIdOptions.SectionName));

        services.Configure<SequentialGuidOptions>(configuration.GetSection(SequentialGuidOptions.SectionName));
        services.Configure<NanoIdOptions>(configuration.GetSection(NanoIdOptions.SectionName));
        services.Configure<SqidsOptions>(configuration.GetSection(SqidsOptions.SectionName));

        // 从配置构建生成器。
        // 此前这里用的是硬编码预设（HighWorkload() / Default()），配置节被完全忽略——
        // 后果是多节点集群里每个节点都按 WorkerId=1 生成，必然产生重复 ID。
        services.AddSingleton(sp =>
            IdGeneratorFactory.CreateSequentialGuidGenerator(sp.GetRequiredService<IOptions<SequentialGuidOptions>>().Value));

        services.AddSingleton(sp =>
            IdGeneratorFactory.CreateSnowflakeIdGenerator(sp.GetRequiredService<IOptions<SnowflakeIdOptions>>().Value));

        return services;
    }
}
