// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Tests.Samples;

namespace XiHan.Framework.Domain.Tests.Entities;

/// <summary>
/// 多租户创建审计实体基类测试
/// </summary>
public class MultiTenantCreationEntityBaseTests
{
    /// <summary>
    /// 租户标识默认落在平台租户
    /// </summary>
    [Fact]
    public void TenantId_ByDefault_IsPlatformTenant()
    {
        var entity = new SampleMultiTenantCreationEntity();

        Assert.Equal(0L, entity.TenantId);
    }

    /// <summary>
    /// 继承的创建审计初值不受多租户扩展影响
    /// </summary>
    [Fact]
    public void Constructor_ByDefault_KeepsInheritedCreationAudit()
    {
        var before = DateTimeOffset.UtcNow;

        var entity = new SampleMultiTenantCreationEntity();

        Assert.InRange(entity.CreatedTime, before, DateTimeOffset.UtcNow);
        Assert.Null(entity.CreatedBy);
        Assert.True(entity.IsTransient());
    }

    /// <summary>
    /// 同时实现创建审计与多租户契约
    /// </summary>
    [Fact]
    public void MultiTenantCreationEntityBase_ImplementsBothContracts()
    {
        var entity = new SampleMultiTenantCreationEntity
        {
            TenantId = 3
        };

        Assert.Equal(3L, entity.TenantId);
        Assert.IsAssignableFrom<IMultiTenantEntity>(entity);
        Assert.IsAssignableFrom<ICreationEntity<long>>(entity);
    }
}
