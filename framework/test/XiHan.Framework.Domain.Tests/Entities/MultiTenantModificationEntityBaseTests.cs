// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Tests.Samples;

namespace XiHan.Framework.Domain.Tests.Entities;

/// <summary>
/// 多租户修改审计实体基类测试
/// </summary>
public class MultiTenantModificationEntityBaseTests
{
    /// <summary>
    /// 租户标识默认落在平台租户
    /// </summary>
    [Fact]
    public void TenantId_ByDefault_IsPlatformTenant()
    {
        var entity = new SampleMultiTenantModificationEntity();

        Assert.Equal(0L, entity.TenantId);
    }

    /// <summary>
    /// 继承的修改审计初值保持为空
    /// </summary>
    [Fact]
    public void Constructor_ByDefault_KeepsInheritedModificationAudit()
    {
        var entity = new SampleMultiTenantModificationEntity();

        Assert.Null(entity.ModifiedTime);
        Assert.Null(entity.ModifiedBy);
        Assert.Equal(0L, entity.ModifiedId);
    }

    /// <summary>
    /// 同时实现修改审计与多租户契约
    /// </summary>
    [Fact]
    public void MultiTenantModificationEntityBase_ImplementsBothContracts()
    {
        var entity = new SampleMultiTenantModificationEntity
        {
            TenantId = 5
        };

        Assert.Equal(5L, entity.TenantId);
        Assert.IsAssignableFrom<IMultiTenantEntity>(entity);
        Assert.IsAssignableFrom<IModificationEntity<long>>(entity);
    }
}
