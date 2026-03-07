#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DistributedJobLockCacheItem
// Guid:b4697a5a-bca2-4be3-a0b9-95d886ecdf5e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 02:07:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Tasks.ScheduledJobs.Locking;

/// <summary>
/// 分布式任务锁缓存项
/// </summary>
[IgnoreMultiTenancy]
public class DistributedJobLockCacheItem
{
    /// <summary>
    /// 锁值
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
