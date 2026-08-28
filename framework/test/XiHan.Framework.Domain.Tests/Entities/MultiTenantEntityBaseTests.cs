// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Tests.Samples;

namespace XiHan.Framework.Domain.Tests.Entities;

/// <summary>
/// 多租户实体基类测试
/// </summary>
/// <remarks>
/// TenantId 是非空 long 且默认 0（平台租户）——这条约定支撑 UNIQUE(TenantId, Code) 复合唯一索引，
/// 一旦默认值变成别的东西，全局记录的唯一性约束就会失效。
/// </remarks>
public class MultiTenantEntityBaseTests
{
    /// <summary>
    /// 租户标识默认落在平台租户
    /// </summary>
    [Fact]
    public void TenantId_ByDefault_IsPlatformTenant()
    {
        var entity = new SampleMultiTenantEntity();

        Assert.Equal(0L, entity.TenantId);
    }

    /// <summary>
    /// 租户标识可写为业务租户
    /// </summary>
    [Fact]
    public void TenantId_WhenAssigned_KeepsAssignedValue()
    {
        var entity = new SampleMultiTenantEntity
        {
            TenantId = 1024
        };

        Assert.Equal(1024L, entity.TenantId);
    }

    /// <summary>
    /// 多租户实体仍然遵循实体基类的瞬态与相等性语义
    /// </summary>
    [Fact]
    public void Equality_WhenSameIdButDifferentTenant_FollowsEntitySemantics()
    {
        var left = new SampleMultiTenantEntity { TenantId = 1 };
        var right = new SampleMultiTenantEntity { TenantId = 2 };
        left.AssignBasicId(5);
        right.AssignBasicId(5);

        // 相等性只看主键，与租户无关；跨租户隔离由查询过滤器负责，不在实体层
        Assert.True(left.Equals(right));
    }

    /// <summary>
    /// 多租户实体同时实现实体与多租户契约
    /// </summary>
    [Fact]
    public void MultiTenantEntityBase_ImplementsTenantContracts()
    {
        var entity = new SampleMultiTenantEntity();

        Assert.IsAssignableFrom<IMultiTenantEntity>(entity);
        Assert.IsAssignableFrom<IEntityBase<long>>(entity);
    }
}
