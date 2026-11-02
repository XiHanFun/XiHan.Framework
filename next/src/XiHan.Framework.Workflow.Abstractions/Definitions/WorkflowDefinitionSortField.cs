// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 定义列表可以按哪个字段排序。
/// </summary>
/// <remarks>
/// 白名单，以 4.x 定义列表能排的列为参照（ADR-0102）；不在其中的值（如请求里给了越界的数字）由 <see cref="IWorkflowDefinitionCatalog.QueryAsync"/> 返回 <c>WORKFLOW_VALIDATION_002</c>。排序字段之后接次序键名字升序、版本降序，规则见 <see cref="WorkflowDefinitionQuery.SortBy"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
[JsonConverter(typeof(JsonStringEnumConverter<WorkflowDefinitionSortField>))]
public enum WorkflowDefinitionSortField
{
    /// <summary>
    /// 名字（4.x 的编码）。
    /// </summary>
    Name = 0,

    /// <summary>
    /// 标题：显示名，没有取名字（4.x 的名称）。
    /// </summary>
    /// <remarks>
    /// 按前 <see cref="WorkflowSortKeys.MaxTitleLength"/> 个字符比较（<see cref="WorkflowSortKeys.Title"/>）。
    /// </remarks>
    Title = 1,

    /// <summary>
    /// 版本。
    /// </summary>
    Version = 2,

    /// <summary>
    /// 状态。
    /// </summary>
    /// <remarks>
    /// 按状态名比较（序数），排出来是按名字的分组，不是生命周期的先后。
    /// </remarks>
    Status = 3,

    /// <summary>
    /// 创建时间。
    /// </summary>
    CreatedAt = 4,

    /// <summary>
    /// 发布时间。
    /// </summary>
    /// <remarks>
    /// 没发布过的为空：空值的位置照数据库，内存实现当作最小。
    /// </remarks>
    PublishedAt = 5,
}
