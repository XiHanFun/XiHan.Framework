// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain;

namespace XiHan.Framework.Persistence.Abstractions.Tests;

/// <summary>
/// 实现全部持久化协议的测试实体，用于验证约定会写入每个协议字段。
/// </summary>
internal sealed class AuditedWidget : IEntity<Guid>, IMultiTenant, IHasCreationTime, IHasModificationTime, IHasConcurrencyStamp, ISoftDeletable
{
    /// <inheritdoc />
    public Guid Id { get; init; }

    /// <inheritdoc />
    public string TenantId { get; set; } = string.Empty;

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAt { get; set; }

    /// <inheritdoc />
    public string ConcurrencyStamp { get; set; } = string.Empty;

    /// <inheritdoc />
    public bool IsDeleted { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedAt { get; set; }
}
