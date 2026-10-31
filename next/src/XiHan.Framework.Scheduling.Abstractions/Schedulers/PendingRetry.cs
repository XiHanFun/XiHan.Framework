// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Scheduling.Abstractions.Jobs;

namespace XiHan.Framework.Scheduling.Abstractions.Schedulers;

/// <summary>
/// 本实例等待中的一次重试。
/// </summary>
/// <param name="Job">作业名。</param>
/// <param name="Data">失败那次运行的数据。</param>
/// <param name="Attempt">这次重试是第几次尝试。</param>
/// <param name="RetryOf">首次运行的 ID。</param>
/// <param name="DueAt">到期时刻。</param>
internal sealed record PendingRetry(JobName Job, JsonObject Data, int Attempt, string RetryOf, DateTimeOffset DueAt);
