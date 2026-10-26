// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.ExternalTasks;

/// <summary>
/// 外部任务失败。
/// </summary>
/// <param name="TaskId">任务 ID。</param>
/// <param name="WorkerId">工作者（必须持有锁）。</param>
/// <param name="Error">错误说明。</param>
/// <param name="RetryAfter">给出时解锁并在该时长后重新可领取；空为节点故障。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ExternalTaskFailure(string TaskId, string WorkerId, string Error, TimeSpan? RetryAfter = null);
