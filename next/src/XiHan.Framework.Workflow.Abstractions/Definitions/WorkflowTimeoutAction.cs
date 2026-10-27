// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 实例级超时（<see cref="WorkflowDefinition.Timeout"/>）到期时的处理。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
[JsonConverter(typeof(JsonStringEnumConverter<WorkflowTimeoutAction>))]
public enum WorkflowTimeoutAction
{
    /// <summary>
    /// 实例故障（可重试、可按 <see cref="WorkflowDefinition.CompensateOnFault"/> 补偿）。
    /// </summary>
    Fault = 0,

    /// <summary>
    /// 实例终止。
    /// </summary>
    Terminate = 1,
}
