// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Tasks.BackgroundJobs.Models;

/// <summary>
/// 后台作业管理审计记录
/// </summary>
/// <param name="JobId">作业标识</param>
/// <param name="Operation">管理操作</param>
/// <param name="Status">结果状态</param>
/// <param name="OccurredAt">发生时间</param>
public sealed record BackgroundJobManagementAuditEntry(Guid JobId, BackgroundJobManagementOperation Operation, BackgroundJobManagementStatus Status, DateTime OccurredAt);
