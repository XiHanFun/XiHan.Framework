// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Jobs;

namespace XiHan.Framework.Scheduling.Abstractions.Schedulers;

/// <summary>
/// 手动触发的请求。
/// </summary>
/// <param name="Name">作业名。</param>
/// <param name="Data">覆盖作业数据的数据。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record JobTriggerRequest(JobName Name, JsonObject? Data = null)
{
    /// <summary>
    /// 从作业名隐式转换。
    /// </summary>
    public static implicit operator JobTriggerRequest(JobName name) => new(name);

    /// <summary>
    /// 从作业名创建。
    /// </summary>
    public static JobTriggerRequest FromName(JobName name) => new(name);
}
