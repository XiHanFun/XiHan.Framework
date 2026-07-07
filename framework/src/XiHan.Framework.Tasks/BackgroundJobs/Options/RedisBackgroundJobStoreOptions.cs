#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RedisBackgroundJobStoreOptions
// Guid:8122e11e-49f8-4069-aaf1-8ef07cbfe81d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/07 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Tasks.BackgroundJobs.Options;

/// <summary>
/// Redis 后台作业存储选项
/// </summary>
public class RedisBackgroundJobStoreOptions
{
    /// <summary>
    /// 键前缀（作业体键 <c>{Prefix}:job:{id}</c>；活跃索引有序集合 <c>{Prefix}:index</c>）
    /// </summary>
    public string KeyPrefix { get; set; } = "XiHan:BackgroundJobs";

    /// <summary>
    /// 已放弃作业的保留天数（放弃后从活跃索引移除，作业体设 TTL 便于事后排查，到期自动清理）
    /// </summary>
    public int AbandonedRetentionDays { get; set; } = 7;

    /// <summary>
    /// 候选加载倍数：每轮从索引取 <c>maxResultCount × 本值</c> 条到期候选，在内存按优先级二次排序后再取 maxResultCount
    /// </summary>
    public int FetchMultiplier { get; set; } = 4;
}
