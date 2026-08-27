// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Tests.Samples;

namespace XiHan.Framework.Domain.Tests.Entities;

/// <summary>
/// 完整审计实体基类测试
/// </summary>
/// <remarks>
/// 完整审计基类同时承担并发版本、创建、修改、删除四组字段的初值契约，
/// 任何一处默认值漂移都会污染持久化行。
/// </remarks>
public class FullAuditedEntityBaseTests
{
    /// <summary>
    /// 无主键版本构造后各审计字段处于初始状态
    /// </summary>
    [Fact]
    public void Constructor_OnKeylessEntity_InitializesAuditFields()
    {
        var before = DateTimeOffset.UtcNow;

        var entity = new SampleFullAuditedEntity();

        Assert.Equal(0L, entity.RowVersion);
        Assert.InRange(entity.CreatedTime, before, DateTimeOffset.UtcNow);
        Assert.Null(entity.ModifiedTime);
        Assert.False(entity.IsDeleted);
        Assert.Null(entity.DeletedTime);
    }

    /// <summary>
    /// 带主键版本无参构造为瞬态实体且审计字段处于初始状态
    /// </summary>
    [Fact]
    public void Constructor_WithKeyAndNoArguments_IsTransientAndInitialized()
    {
        var before = DateTimeOffset.UtcNow;

        var entity = new SampleFullAuditedEntityWithKey();

        Assert.True(entity.IsTransient());
        Assert.Equal(0L, entity.RowVersion);
        Assert.InRange(entity.CreatedTime, before, DateTimeOffset.UtcNow);
        Assert.Equal(0L, entity.CreatedId);
        Assert.Null(entity.CreatedBy);
        Assert.Null(entity.ModifiedTime);
        Assert.Equal(0L, entity.ModifiedId);
        Assert.Null(entity.ModifiedBy);
        Assert.False(entity.IsDeleted);
        Assert.Null(entity.DeletedTime);
        Assert.Equal(0L, entity.DeletedId);
        Assert.Null(entity.DeletedBy);
    }

    /// <summary>
    /// 单参构造写入主键
    /// </summary>
    [Fact]
    public void Constructor_WithBasicId_SetsId()
    {
        var entity = new SampleFullAuditedEntityWithKey(71);

        Assert.Equal(71L, entity.BasicId);
        Assert.False(entity.IsTransient());
        Assert.Equal(0L, entity.CreatedId);
    }

    /// <summary>
    /// 双参构造同时写入主键与创建者主键
    /// </summary>
    [Fact]
    public void Constructor_WithBasicIdAndCreatedId_SetsBoth()
    {
        var entity = new SampleFullAuditedEntityWithKey(71, 81);

        Assert.Equal(71L, entity.BasicId);
        Assert.Equal(81L, entity.CreatedId);
        Assert.False(entity.IsDeleted);
    }

    /// <summary>
    /// 主键相同的完整审计实体按实体语义相等，与审计字段无关
    /// </summary>
    [Fact]
    public void Equals_WhenSameIdButDifferentAuditFields_ReturnsTrue()
    {
        var left = new SampleFullAuditedEntityWithKey(71)
        {
            CreatedBy = "a",
            RowVersion = 1
        };
        var right = new SampleFullAuditedEntityWithKey(71)
        {
            CreatedBy = "b",
            RowVersion = 2
        };

        Assert.True(left.Equals(right));
    }

    /// <summary>
    /// 完整审计接口层级完整
    /// </summary>
    [Fact]
    public void FullAuditedEntityBase_ImplementsFullAuditContracts()
    {
        var keyless = new SampleFullAuditedEntity();
        var keyed = new SampleFullAuditedEntityWithKey();

        Assert.IsAssignableFrom<IFullAuditedEntity>(keyless);
        Assert.IsAssignableFrom<ICreationEntity>(keyless);
        Assert.IsAssignableFrom<IModificationEntity>(keyless);
        Assert.IsAssignableFrom<IDeletionEntity>(keyless);
        Assert.IsAssignableFrom<IEntityBase>(keyless);

        Assert.IsAssignableFrom<IFullAuditedEntity<long>>(keyed);
        Assert.IsAssignableFrom<ICreationEntity<long>>(keyed);
        Assert.IsAssignableFrom<IModificationEntity<long>>(keyed);
        Assert.IsAssignableFrom<IDeletionEntity<long>>(keyed);
        Assert.IsAssignableFrom<IEntityBase<long>>(keyed);
    }
}
