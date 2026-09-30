// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Tasks.BackgroundJobs.Abstractions;

/// <summary>
/// 后台作业标记接口（用于类型定位，不直接实现）
/// </summary>
public interface IBackgroundJob;

/// <summary>
/// 异步后台作业处理器
/// </summary>
/// <typeparam name="TArgs">作业参数类型（同时作为作业的稳定标识来源）</typeparam>
public interface IAsyncBackgroundJob<in TArgs> : IBackgroundJob
{
    /// <summary>
    /// 执行作业
    /// </summary>
    /// <param name="args">作业参数</param>
    /// <returns>任务</returns>
    Task ExecuteAsync(TArgs args);

    /// <summary>
    /// 执行作业（可观察取消）
    /// </summary>
    /// <param name="args">作业参数</param>
    /// <param name="cancellationToken">取消令牌：宿主停止、失去租约或管理端请求取消时触发</param>
    /// <returns>任务</returns>
    Task ExecuteAsync(TArgs args, CancellationToken cancellationToken)
    {
        return ExecuteAsync(args);
    }
}
