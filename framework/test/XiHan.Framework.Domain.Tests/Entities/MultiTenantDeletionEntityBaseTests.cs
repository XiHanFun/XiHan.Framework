// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Tests.Samples;

namespace XiHan.Framework.Domain.Tests.Entities;

/// <summary>
/// 多租户删除审计实体基类测试
/// </summary>
public class MultiTenantDeletionEntityBaseTests
{
    /// <summary>
    /// 租户标识默认落在平台租户
    /// </summary>
    [Fact]
    public void TenantId_ByDefault_IsPlatformTenant()
    {
        var entity = new SampleMultiTenantDeletionEntity();

        Assert.Equal(0L, entity.TenantId);
    }

    /// <summary>
    /// 继承的软删除标记默认为未删除
    /// </summary>
    [Fact]
    public void Constructor_ByDefault_KeepsInheritedDeletionAudit()
    {
        var entity = new SampleMultiTenantDeletionEntity();

        Assert.False(entity.IsDeleted);
        Assert.Null(entity.DeletedTime);
        Assert.Null(entity.DeletedBy);
    }

    /// <summary>
    /// 同时实现删除审计与多租户契约
    /// </summary>
    [Fact]
    public void MultiTenantDeletionEntityBase_ImplementsBothContracts()
    {
        var entity = new SampleMultiTenantDeletionEntity
        {
            TenantId = 7
        };

        Assert.Equal(7L, entity.TenantId);
        Assert.IsAssignableFrom<IMultiTenantEntity>(entity);
        Assert.IsAssignableFrom<IDeletionEntity<long>>(entity);
        Assert.IsAssignableFrom<ISoftDelete>(entity);
    }
}
