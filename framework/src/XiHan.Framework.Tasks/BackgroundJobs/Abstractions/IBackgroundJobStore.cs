// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundJobs.Models;

namespace XiHan.Framework.Tasks.BackgroundJobs.Abstractions;

/// <summary>
/// 后台作业持久化存储端口
/// </summary>
/// <remarks>
/// 默认提供进程内内存实现（单实例）。要支持跨实例可靠投递，应用侧实现基于数据库 / Redis 的存储，
/// 并在 <see cref="GetWaitingJobsAsync"/> 中做原子领取以避免多实例重复执行。
/// </remarks>
public interface IBackgroundJobStore
{
    /// <summary>
    /// 按标识查找作业
    /// </summary>
    /// <param name="jobId">作业标识</param>
    /// <returns>作业信息，不存在则为 null</returns>
    Task<BackgroundJobInfo?> FindAsync(Guid jobId);

    /// <summary>
    /// 插入作业
    /// </summary>
    /// <param name="jobInfo">作业信息</param>
    /// <returns>任务</returns>
    Task InsertAsync(BackgroundJobInfo jobInfo);

    /// <summary>
    /// 获取待执行作业
    /// </summary>
    /// <remarks>
    /// 语义契约：过滤 <c>ApplicationName 匹配 &amp;&amp; !IsAbandoned &amp;&amp; NextTryTime &lt;= 当前时间</c>；
    /// 排序 <c>Priority 降序, TryCount 升序, NextTryTime 升序</c>；最多返回 <paramref name="maxResultCount"/> 条。
    /// </remarks>
    /// <param name="applicationName">应用名（为空表示不区分）</param>
    /// <param name="maxResultCount">最大返回数量</param>
    /// <returns>待执行作业列表</returns>
    Task<List<BackgroundJobInfo>> GetWaitingJobsAsync(string? applicationName, int maxResultCount);

    /// <summary>
    /// 删除作业（执行成功后调用）
    /// </summary>
    /// <param name="jobId">作业标识</param>
    /// <returns>任务</returns>
    Task DeleteAsync(Guid jobId);

    /// <summary>
    /// 更新作业（失败退避 / 放弃后回写）
    /// </summary>
    /// <param name="jobInfo">作业信息</param>
    /// <returns>任务</returns>
    Task UpdateAsync(BackgroundJobInfo jobInfo);
}
