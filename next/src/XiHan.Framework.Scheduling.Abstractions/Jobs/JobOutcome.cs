// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Jobs;

/// <summary>
/// 一次运行的结果：起止时刻、触发来源与调用结果。
/// </summary>
/// <param name="Run">运行。</param>
/// <param name="StartedAt">开始时刻。</param>
/// <param name="CompletedAt">结束时刻。</param>
/// <param name="Result">调用结果。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record JobOutcome(JobRun Run, DateTimeOffset StartedAt, DateTimeOffset CompletedAt, Result Result);
