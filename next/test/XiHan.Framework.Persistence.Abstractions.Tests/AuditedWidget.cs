// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain;

namespace XiHan.Framework.Persistence.Abstractions.Tests;

internal sealed class AuditedWidget : IEntity<Guid>, IMultiTenant, IHasCreationTime, IHasModificationTime, IHasConcurrencyStamp, ISoftDeletable
{
    public Guid Id { get; init; }

    public string TenantId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public string ConcurrencyStamp { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
