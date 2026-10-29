// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Database.Tenants;

/// <summary>
/// 租户目录的存储行。
/// </summary>
/// <remarks>
/// 主键是租户 ID（文法由 <c>TenantIdentifier</c> 校验），显示名、启用标记各一列，属性整份放在 JSON 列里。平台级数据：不实现 <c>IMultiTenant</c>、不软删除，读写不受当前调用租户影响。EF Core 应用在自己的 <c>DbContext</c> 里 <c>modelBuilder.Entity&lt;TenantRecord&gt;()</c>，SqlSugar 按约定映射。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class TenantRecord : IEntity<string>, IHasCreationTime, IHasModificationTime, IHasConcurrencyStamp
{
    /// <inheritdoc />
    [StringLength(128)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 显示名。
    /// </summary>
    [StringLength(256)]
    public string? DisplayName { get; set; }

    /// <summary>
    /// 是否启用。
    /// </summary>
    /// <remarks>
    /// 禁用的租户解析时被拒绝。
    /// </remarks>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 租户属性（键区分大小写的字符串字典）的 JSON 对象。
    /// </summary>
    /// <remarks>
    /// 长文本列。
    /// </remarks>
    [LongText]
    public string PropertiesJson { get; set; } = TenantRecordMapping.EmptyProperties;

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAt { get; set; }

    /// <inheritdoc />
    [StringLength(32)]
    public string ConcurrencyStamp { get; set; } = string.Empty;
}
