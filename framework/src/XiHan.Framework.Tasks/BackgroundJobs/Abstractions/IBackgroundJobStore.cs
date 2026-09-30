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
/// 租约与管理相关成员默认不支持（<see cref="SupportsJobLease"/> 与 <see cref="SupportsJobManagement"/> 为 false）。
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

    /// <summary>
    /// 是否支持逐作业租约（领取时填写 ClaimToken 与 LeaseExpiresAt，并实现按令牌续租与回写）
    /// </summary>
    bool SupportsJobLease => false;

    /// <summary>
    /// 续租：令牌匹配且租约未到期时延长租约
    /// </summary>
    /// <param name="lease">当前租约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>续租后的租约（携带最新的取消请求标记）；令牌不匹配、租约已到期或作业不存在时返回 null</returns>
    Task<BackgroundJobLease?> TryRenewLeaseAsync(BackgroundJobLease lease, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("当前后台作业存储不支持作业租约。");
    }

    /// <summary>
    /// 按令牌完成作业（删除）；令牌不匹配时不做任何变更
    /// </summary>
    /// <param name="lease">当前租约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否命中</returns>
    Task<bool> TryCompleteAsync(BackgroundJobLease lease, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("当前后台作业存储不支持作业租约。");
    }

    /// <summary>
    /// 按令牌回写作业（失败退避 / 放弃 / 取消）并结束租约；令牌不匹配时不做任何变更
    /// </summary>
    /// <param name="jobInfo">作业信息</param>
    /// <param name="lease">当前租约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否命中</returns>
    Task<bool> TryUpdateAsync(BackgroundJobInfo jobInfo, BackgroundJobLease lease, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("当前后台作业存储不支持作业租约。");
    }

    /// <summary>
    /// 按令牌释放租约，作业保持待执行；令牌不匹配时不做任何变更
    /// </summary>
    /// <remarks>
    /// 只清除领取令牌与租约到期时间，必须保留 <see cref="BackgroundJobInfo.IsCancellationRequested"/>，下次领取时仍能读到已登记的取消请求。
    /// </remarks>
    /// <param name="lease">当前租约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    Task ReleaseLeaseAsync(BackgroundJobLease lease, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("当前后台作业存储不支持作业租约。");
    }

    /// <summary>
    /// 是否支持作业管理（重试与取消）
    /// </summary>
    bool SupportsJobManagement => false;

    /// <summary>
    /// 把已放弃的作业重新排入待执行：清除放弃与取消标记、尝试次数归零、下次执行时间设为当前时间、结束租约
    /// </summary>
    /// <param name="jobId">作业标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Rescheduled / NoChange（未处于放弃状态）/ NotFound / NotSupported</returns>
    Task<BackgroundJobManagementStatus> RetryAbandonedAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(BackgroundJobManagementStatus.NotSupported);
    }

    /// <summary>
    /// 请求取消作业：持有有效租约的作业登记取消请求；其余未放弃的作业直接标记放弃并结束租约
    /// </summary>
    /// <param name="jobId">作业标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>CancellationRequested / Cancelled / NoChange（已放弃或已请求）/ NotFound / NotSupported</returns>
    Task<BackgroundJobManagementStatus> RequestCancellationAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(BackgroundJobManagementStatus.NotSupported);
    }
}
