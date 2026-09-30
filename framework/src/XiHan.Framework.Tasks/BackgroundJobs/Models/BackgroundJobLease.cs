// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Tasks.BackgroundJobs.Models;

/// <summary>
/// 后台作业租约
/// </summary>
/// <param name="JobId">作业标识</param>
/// <param name="Token">领取令牌，每次领取重新生成</param>
/// <param name="ExpiresAt">租约到期时间（与存储时钟同基准）</param>
/// <param name="IsCancellationRequested">是否已被请求取消</param>
public sealed record BackgroundJobLease(Guid JobId, string Token, DateTime ExpiresAt, bool IsCancellationRequested = false);
