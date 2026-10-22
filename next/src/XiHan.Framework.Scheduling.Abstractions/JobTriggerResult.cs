// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions;

/// <summary>
/// 批量触发里一项的结果。
/// </summary>
/// <param name="Name">作业名。</param>
/// <param name="Outcome">运行结果：作业的调用结果，或未登记 / 正在运行的失败。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record JobTriggerResult(JobName Name, Result Outcome);
