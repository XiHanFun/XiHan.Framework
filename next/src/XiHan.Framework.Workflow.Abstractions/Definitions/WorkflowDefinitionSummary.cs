// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 定义列表里的一行摘要
/// </summary>
/// <remarks>
/// 只有列表要显示与过滤的字段，不含节点、转移、变量与扩展属性；要整份定义按 <see cref="Id"/> 调 <see cref="IWorkflowDefinitionCatalog.FindAsync"/>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WorkflowDefinitionSummary
{
    /// <summary>
    /// 定义 ID
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 定义名
    /// </summary>
    public required WorkflowName Name { get; init; }

    /// <summary>
    /// 版本号
    /// </summary>
    public int Version { get; init; } = 1;

    /// <summary>
    /// 显示名
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// 说明
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 分类
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// 状态
    /// </summary>
    public WorkflowDefinitionStatus Status { get; init; }

    /// <summary>
    /// 所属租户
    /// </summary>
    /// <remarks>
    /// 平台级为 <see langword="null"/>
    /// </remarks>
    public string? TenantId { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// 发布时间
    /// </summary>
    public DateTimeOffset? PublishedAt { get; init; }

    /// <summary>
    /// 显示名或名字
    /// </summary>
    public string Title => string.IsNullOrWhiteSpace(DisplayName) ? Name.Value : DisplayName;

    /// <summary>
    /// 从整份定义取摘要
    /// </summary>
    public static WorkflowDefinitionSummary From(WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new WorkflowDefinitionSummary
        {
            Id = definition.Id,
            Name = definition.Name,
            Version = definition.Version,
            DisplayName = definition.DisplayName,
            Description = definition.Description,
            Category = definition.Category,
            Status = definition.Status,
            TenantId = definition.TenantId,
            CreatedAt = definition.CreatedAt,
            PublishedAt = definition.PublishedAt,
        };
    }
}
