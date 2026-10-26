// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Database.Entities;

/// <summary>
/// 工作流定义的存储行：过滤与排序用的列加整份定义的 JSON 文档（<c>WorkflowJson.Serialize</c>）。不实现 <c>IMultiTenant</c>，租户只是普通列，存储不按租户过滤。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WorkflowDefinitionRecord : IEntity<string>
{
    /// <inheritdoc />
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 定义名。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 版本号。
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// 状态名（<c>WorkflowDefinitionStatus</c> 的名字）。
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 租户。
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// 创建时间。
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// 发布时间。
    /// </summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>
    /// 整份定义的 JSON；长文本列。
    /// </summary>
    public string Document { get; set; } = string.Empty;
}
