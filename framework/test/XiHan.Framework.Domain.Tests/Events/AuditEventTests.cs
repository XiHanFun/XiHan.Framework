// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Events;
using XiHan.Framework.Domain.Events.Abstracts;
using XiHan.Framework.Domain.Tests.Samples;

namespace XiHan.Framework.Domain.Tests.Events;

/// <summary>
/// 审计事件测试
/// </summary>
/// <remarks>
/// 审计事件承载「哪个实体、什么类型、什么时候」三段信息，全部只读；
/// 修改事件额外带原始实体快照，且该参数是可选的。
/// </remarks>
public class AuditEventTests
{
    /// <summary>
    /// 创建事件保留实体引用与描述信息
    /// </summary>
    [Fact]
    public void EntityCreatedEvent_KeepsEntityAndDescriptors()
    {
        var entity = new SampleEntity(1);
        var before = DateTimeOffset.UtcNow;

        var auditEvent = new EntityCreatedEvent(entity, nameof(SampleEntity), "1");

        Assert.Same(entity, auditEvent.Entity);
        Assert.Equal(nameof(SampleEntity), auditEvent.EntityType);
        Assert.Equal("1", auditEvent.EntityId);
        Assert.InRange(auditEvent.Timestamp, before, DateTimeOffset.UtcNow);
        Assert.Equal(TimeSpan.Zero, auditEvent.Timestamp.Offset);
    }

    /// <summary>
    /// 修改事件默认不携带原始实体
    /// </summary>
    [Fact]
    public void EntityModifiedEvent_WithoutOriginal_LeavesOriginalNull()
    {
        var entity = new SampleEntity(1);

        var auditEvent = new EntityModifiedEvent(entity, nameof(SampleEntity), "1");

        Assert.Same(entity, auditEvent.Entity);
        Assert.Null(auditEvent.OriginalEntity);
    }

    /// <summary>
    /// 修改事件可携带原始实体快照
    /// </summary>
    [Fact]
    public void EntityModifiedEvent_WithOriginal_KeepsOriginal()
    {
        var current = new SampleEntity(1) { Name = "new" };
        var original = new SampleEntity(1) { Name = "old" };

        var auditEvent = new EntityModifiedEvent(current, nameof(SampleEntity), "1", original);

        Assert.Same(current, auditEvent.Entity);
        Assert.Same(original, auditEvent.OriginalEntity);
    }

    /// <summary>
    /// 删除事件保留实体引用与描述信息
    /// </summary>
    [Fact]
    public void EntityDeletedEvent_KeepsEntityAndDescriptors()
    {
        var entity = new SampleEntity(1);

        var auditEvent = new EntityDeletedEvent(entity, nameof(SampleEntity), "1");

        Assert.Same(entity, auditEvent.Entity);
        Assert.Equal(nameof(SampleEntity), auditEvent.EntityType);
        Assert.Equal("1", auditEvent.EntityId);
    }

    /// <summary>
    /// 三类审计事件共享同一个抽象基类
    /// </summary>
    [Fact]
    public void AuditEvents_ShareCommonBaseType()
    {
        var entity = new SampleEntity(1);

        Assert.IsAssignableFrom<AuditEvent>(new EntityCreatedEvent(entity, "T", "1"));
        Assert.IsAssignableFrom<AuditEvent>(new EntityModifiedEvent(entity, "T", "1"));
        Assert.IsAssignableFrom<AuditEvent>(new EntityDeletedEvent(entity, "T", "1"));
    }

    /// <summary>
    /// 审计事件不是领域事件，两套管线互不串用
    /// </summary>
    /// <remarks>
    /// AuditEvent 未实现 IDomainEvent，因此不能直接塞进聚合根的事件缓冲区——这条边界要锁住。
    /// </remarks>
    [Fact]
    public void AuditEvent_IsNotDomainEvent()
    {
        Assert.False(typeof(IDomainEvent).IsAssignableFrom(typeof(AuditEvent)));
    }

    /// <summary>
    /// 审计事件的描述属性全部只读
    /// </summary>
    [Fact]
    public void AuditEvent_Properties_AreReadOnly()
    {
        var type = typeof(AuditEvent);

        Assert.Null(type.GetProperty(nameof(AuditEvent.Entity))!.SetMethod);
        Assert.Null(type.GetProperty(nameof(AuditEvent.EntityType))!.SetMethod);
        Assert.Null(type.GetProperty(nameof(AuditEvent.EntityId))!.SetMethod);
        Assert.Null(type.GetProperty(nameof(AuditEvent.Timestamp))!.SetMethod);
    }
}
