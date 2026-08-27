// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Aggregates.Abstracts;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Tests.Samples;

namespace XiHan.Framework.Domain.Tests.Aggregates;

/// <summary>
/// 多租户聚合根基类测试
/// </summary>
/// <remarks>
/// 聚合根必须实现 IMultiTenantEntity，否则全局租户查询过滤器命中不到聚合根，会造成跨租户读。
/// </remarks>
public class MultiTenantAggregateRootBaseTests
{
    /// <summary>
    /// 租户标识默认落在平台租户
    /// </summary>
    [Fact]
    public void TenantId_ByDefault_IsPlatformTenant()
    {
        var aggregate = new SampleMultiTenantAggregateRoot();

        Assert.Equal(0L, aggregate.TenantId);
    }

    /// <summary>
    /// 租户标识可写为业务租户
    /// </summary>
    [Fact]
    public void TenantId_WhenAssigned_KeepsAssignedValue()
    {
        var aggregate = new SampleMultiTenantAggregateRoot
        {
            TenantId = 2048
        };

        Assert.Equal(2048L, aggregate.TenantId);
    }

    /// <summary>
    /// 多租户聚合根保留完整的领域事件能力
    /// </summary>
    [Fact]
    public void MultiTenantAggregate_KeepsDomainEventCapability()
    {
        var aggregate = new SampleMultiTenantAggregateRoot();

        aggregate.RaiseLocal(new SampleCreatedEvent("a"));

        Assert.Single(aggregate.GetLocalEvents());

        aggregate.ClearLocalEvents();

        Assert.Empty(aggregate.GetLocalEvents());
    }

    /// <summary>
    /// 多租户聚合根保留完整审计与实体语义
    /// </summary>
    [Fact]
    public void MultiTenantAggregate_KeepsAuditAndEntitySemantics()
    {
        var before = DateTimeOffset.UtcNow;

        var aggregate = new SampleMultiTenantAggregateRoot();

        Assert.InRange(aggregate.CreatedTime, before, DateTimeOffset.UtcNow);
        Assert.False(aggregate.IsDeleted);
        Assert.True(aggregate.IsTransient());

        aggregate.AssignBasicId(9);

        Assert.False(aggregate.IsTransient());
        Assert.Equal(9L, aggregate.BasicId);
    }

    /// <summary>
    /// 多租户聚合根同时满足聚合根与多租户契约
    /// </summary>
    [Fact]
    public void MultiTenantAggregate_ImplementsBothContracts()
    {
        var aggregate = new SampleMultiTenantAggregateRoot();

        Assert.IsAssignableFrom<IMultiTenantEntity>(aggregate);
        Assert.IsAssignableFrom<IAggregateRoot<long>>(aggregate);
    }
}
