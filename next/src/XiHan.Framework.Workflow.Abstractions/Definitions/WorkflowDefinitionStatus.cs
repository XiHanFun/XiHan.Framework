// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 定义的状态：草稿可改可发布，已发布可启动与停用，归档只读。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
[JsonConverter(typeof(JsonStringEnumConverter<WorkflowDefinitionStatus>))]
public enum WorkflowDefinitionStatus
{
    /// <summary>
    /// 草稿：可修改、删除、发布。
    /// </summary>
    Draft = 0,

    /// <summary>
    /// 已发布：可启动实例、可停用。
    /// </summary>
    Published = 1,

    /// <summary>
    /// 已停用：不再启动新实例，已有实例继续。
    /// </summary>
    Disabled = 2,

    /// <summary>
    /// 已归档：只读。
    /// </summary>
    Archived = 3,
}
