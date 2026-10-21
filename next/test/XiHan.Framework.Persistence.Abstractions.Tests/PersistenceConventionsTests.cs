// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Time.Testing;

namespace XiHan.Framework.Persistence.Abstractions.Tests;

public sealed class PersistenceConventionsTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 21, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ApplyOnAdd_WritesTenantCreationTimeAndStamp()
    {
        var entity = new AuditedWidget { Id = Guid.NewGuid() };

        var result = PersistenceConventions.ApplyOnAdd(entity, "acme", _now);

        Assert.True(result.IsSuccess);
        Assert.Equal("acme", entity.TenantId);
        Assert.Equal(_now, entity.CreatedAt);
        Assert.Equal(32, entity.ConcurrencyStamp.Length);
    }

    [Fact]
    public void ApplyOnAdd_MultiTenantWithoutTenant_ReturnsTenantRequired()
    {
        var entity = new AuditedWidget { Id = Guid.NewGuid() };

        var result = PersistenceConventions.ApplyOnAdd(entity, tenantId: null, _now);

        Assert.True(result.IsFailure);
        Assert.Equal(PersistenceErrors.TenantRequired, result.Error!.Code);
        Assert.True(PersistenceConventions.ApplyOnAdd(new Widget { Id = Guid.NewGuid() }, tenantId: null, _now).IsSuccess);
    }

    [Fact]
    public void ApplyOnUpdate_WritesModificationTimeAndRefreshesStamp()
    {
        var entity = new AuditedWidget { Id = Guid.NewGuid(), ConcurrencyStamp = "old" };

        PersistenceConventions.ApplyOnUpdate(entity, _now);

        Assert.Equal(_now, entity.ModifiedAt);
        Assert.NotEqual("old", entity.ConcurrencyStamp);
    }

    [Fact]
    public void TrySoftDelete_MarksSoftDeletable_AndReportsPhysicalOtherwise()
    {
        var soft = new AuditedWidget { Id = Guid.NewGuid() };
        var plain = new Widget { Id = Guid.NewGuid() };

        Assert.True(PersistenceConventions.TrySoftDelete(soft, _now));
        Assert.True(soft.IsDeleted);
        Assert.Equal(_now, soft.DeletedAt);
        Assert.True(PersistenceConventions.IsDeleted(soft));

        Assert.False(PersistenceConventions.TrySoftDelete(plain, _now));
        Assert.False(PersistenceConventions.IsDeleted(plain));
    }

    [Fact]
    public void IsVisibleTo_FiltersByTenantOnlyForMultiTenantEntities()
    {
        var tenantEntity = new AuditedWidget { Id = Guid.NewGuid(), TenantId = "acme" };
        var plain = new Widget { Id = Guid.NewGuid() };

        Assert.True(PersistenceConventions.IsVisibleTo(tenantEntity, "acme"));
        Assert.False(PersistenceConventions.IsVisibleTo(tenantEntity, "other"));
        Assert.False(PersistenceConventions.IsVisibleTo(tenantEntity, null));
        Assert.True(PersistenceConventions.IsVisibleTo(plain, null));
        Assert.True(PersistenceConventions.IsVisibleTo(plain, "acme"));
    }

    [Fact]
    public void GuidV7IdGenerator_UsesTimeProvider_AndIsOrdered()
    {
        var clock = new FakeTimeProvider(_now);
        var generator = new GuidV7IdGenerator(clock);

        var first = generator.NewId();
        clock.Advance(TimeSpan.FromMilliseconds(5));
        var second = generator.NewId();

        Assert.Equal(7, first.Version);
        Assert.True(string.CompareOrdinal(first.ToString(), second.ToString()) < 0);
    }
}
