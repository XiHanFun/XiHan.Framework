// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;

namespace XiHan.Framework.Workflow.Database.Entities;

/// <summary>
/// 工作流定义存储行的公共列。
/// </summary>
/// <remarks>
/// 过滤与排序用的列（含定义列表的分类与关键字查询列）加整份定义的 JSON 文档（<c>WorkflowJson.Serialize</c>）。两张表共用它：租户分区的 <see cref="WorkflowDefinitionRecord"/>（实现 <c>IMultiTenant</c>，随租户分库）与平台分区的 <see cref="PlatformWorkflowDefinitionRecord"/>（平台库）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class WorkflowDefinitionRow : IEntity<string>
{
    /// <inheritdoc />
    [StringLength(128)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 定义名。
    /// </summary>
    [StringLength(256)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 版本号。
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// 状态名（<c>WorkflowDefinitionStatus</c> 的名字）。
    /// </summary>
    [StringLength(32)]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 状态在生命周期里的序号。
    /// </summary>
    /// <remarks>
    /// <c>Draft</c> 0、<c>Published</c> 1、<c>Disabled</c> 2、<c>Archived</c> 3（同 4.x 按状态枚举的整数值排，ADR-0103）。按状态排序时 SQL 对它 <c>ORDER BY</c>，而不是对状态名（那是字母序）。存储每次整行写入时随 <see cref="Status"/> 一起写（新增、状态变更、迁移、跳转、重试都经整行写入），是存储内部的细节，不是公开常量。可空只为给已有行的表加列：升级前写入的行为空，迁移时按 <see cref="Status"/> 回填，没回填的行按状态排序时位置照数据库的空值规则。
    /// </remarks>
    public int? StatusOrder { get; set; }

    /// <summary>
    /// 分类。
    /// </summary>
    /// <remarks>
    /// 定义列表按它精确过滤，与 <c>WorkflowDefinition.MaxCategoryLength</c> 同宽。可空：升级前写入的行为空，迁移时从文档回填（ADR-0096）。
    /// </remarks>
    [StringLength(128)]
    public string? Category { get; set; }

    /// <summary>
    /// 关键字查询列。
    /// </summary>
    /// <remarks>
    /// 名字、显示名、说明各自折叠后以 U+001F 拼成（<c>WorkflowKeyword.Join</c>），定义列表的关键字在 SQL 里对它做包含判断。存储写入的行总有值；可空只为给已有行的表加列（升级前写入的行为空，迁移时从文档回填）。
    /// </remarks>
    [LongText]
    public string? SearchText { get; set; }

    /// <summary>
    /// 排序用的标题。
    /// </summary>
    /// <remarks>
    /// 显示名（没有取名字）的前 256 个字符（<c>WorkflowSortKeys.Title</c>），按标题排序时 SQL 对它 <c>ORDER BY</c>（ADR-0102）；定长是因为长文本在 Oracle 上不能排序。存储写入的行总有值；可空只为给已有行的表加列（升级前写入的行为空，迁移时从文档回填，没回填的行按标题排序时位置照数据库的空值规则）。
    /// </remarks>
    [StringLength(WorkflowSortKeys.MaxTitleLength)]
    public string? Title { get; set; }

    /// <summary>
    /// 创建时间。
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// 发布时间。
    /// </summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>
    /// 整份定义的 JSON。
    /// </summary>
    /// <remarks>
    /// 长文本列。
    /// </remarks>
    [LongText]
    public string Document { get; set; } = string.Empty;
}
