// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Jobs;

namespace XiHan.Framework.Scheduling.Abstractions.Storage;

/// <summary>
/// 作业记录：定义的持久化形态（作业类型只记键）加运行期状态。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class JobRecord
{
    /// <summary>
    /// 作业名。
    /// </summary>
    public required JobName Name { get; set; }

    /// <summary>
    /// 作业类型键（<see cref="JobTypeRegistry"/>）。
    /// </summary>
    public required string JobTypeName { get; set; }

    /// <summary>
    /// 运行时传给作业的数据。
    /// </summary>
    public JsonObject Data { get; set; } = [];

    /// <summary>
    /// 允许多次运行重叠。
    /// </summary>
    public bool AllowConcurrent { get; set; }

    /// <summary>
    /// 每个实例各自运行。
    /// </summary>
    public bool RunOnEveryInstance { get; set; }

    /// <summary>
    /// 节点失联时重新入队。
    /// </summary>
    public bool RequestsRecovery { get; set; }

    /// <summary>
    /// 说明。
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 是否暂停。
    /// </summary>
    public bool Paused { get; set; }

    /// <summary>
    /// 不允许并发时正在运行的运行 ID；没有为空。
    /// </summary>
    public string? ActiveRunId { get; set; }

    /// <summary>
    /// 来源。
    /// </summary>
    public JobSource Source { get; set; }

    /// <summary>
    /// 最近写入时刻。
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// 深拷贝。
    /// </summary>
    public JobRecord Clone() => new()
    {
        Name = Name,
        JobTypeName = JobTypeName,
        Data = (JsonObject)Data.DeepClone(),
        AllowConcurrent = AllowConcurrent,
        RunOnEveryInstance = RunOnEveryInstance,
        RequestsRecovery = RequestsRecovery,
        Description = Description,
        Paused = Paused,
        ActiveRunId = ActiveRunId,
        Source = Source,
        UpdatedAt = UpdatedAt,
    };
}
