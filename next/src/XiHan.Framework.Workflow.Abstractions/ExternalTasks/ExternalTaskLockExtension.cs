// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.ExternalTasks;

/// <summary>
/// 延长外部任务的锁。
/// </summary>
/// <param name="TaskId">任务 ID。</param>
/// <param name="WorkerId">工作者（必须持有锁）。</param>
/// <param name="LockDuration">从现在起的新锁时长。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ExternalTaskLockExtension(string TaskId, string WorkerId, TimeSpan LockDuration);
