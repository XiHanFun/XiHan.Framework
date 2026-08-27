// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Tests.Samples;

namespace XiHan.Framework.Domain.Tests.Entities;

/// <summary>
/// 多租户完整审计实体基类测试
/// </summary>
public class MultiTenantFullAuditedEntityBaseTests
{
    /// <summary>
    /// 租户标识默认落在平台租户
    /// </summary>
    [Fact]
    public void TenantId_ByDefault_IsPlatformTenant()
    {
        var entity = new SampleMultiTenantFullAuditedEntity();

        Assert.Equal(0L, entity.TenantId);
    }

    /// <summary>
    /// 继承的完整审计初值不受多租户扩展影响
    /// </summary>
    [Fact]
    public void Constructor_ByDefault_KeepsInheritedFullAudit()
    {
        var before = DateTimeOffset.UtcNow;

        var entity = new SampleMultiTenantFullAuditedEntity();

        Assert.Equal(0L, entity.RowVersion);
        Assert.InRange(entity.CreatedTime, before, DateTimeOffset.UtcNow);
        Assert.Null(entity.ModifiedTime);
        Assert.False(entity.IsDeleted);
        Assert.Null(entity.DeletedTime);
        Assert.True(entity.IsTransient());
    }

    /// <summary>
    /// 同时实现完整审计与多租户契约
    /// </summary>
    [Fact]
    public void MultiTenantFullAuditedEntityBase_ImplementsBothContracts()
    {
        var entity = new SampleMultiTenantFullAuditedEntity
        {
            TenantId = 9
        };

        Assert.Equal(9L, entity.TenantId);
        Assert.IsAssignableFrom<IMultiTenantEntity>(entity);
        Assert.IsAssignableFrom<IFullAuditedEntity<long>>(entity);
    }
}
