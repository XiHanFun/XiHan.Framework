// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Tests.Samples;

namespace XiHan.Framework.Domain.Tests.Entities;

/// <summary>
/// 创建审计实体基类测试
/// </summary>
/// <remarks>
/// 只断言「构造即写入 UTC 创建时间」这条契约，不锁具体时刻。
/// </remarks>
public class CreationEntityBaseTests
{
    /// <summary>
    /// 构造后创建时间落在 UTC 当下
    /// </summary>
    [Fact]
    public void Constructor_ByDefault_SetsCreatedTimeToUtcNow()
    {
        var before = DateTimeOffset.UtcNow;

        var entity = new SampleCreationEntity();

        var after = DateTimeOffset.UtcNow;
        Assert.InRange(entity.CreatedTime, before, after);
        Assert.Equal(TimeSpan.Zero, entity.CreatedTime.Offset);
    }

    /// <summary>
    /// 创建时间可被审计管线覆盖
    /// </summary>
    [Fact]
    public void CreatedTime_WhenAssigned_KeepsAssignedValue()
    {
        var expected = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var entity = new SampleCreationEntity
        {
            CreatedTime = expected
        };

        Assert.Equal(expected, entity.CreatedTime);
    }

    /// <summary>
    /// 无参构造的带主键版本是瞬态实体且已写入创建时间
    /// </summary>
    [Fact]
    public void Constructor_WithKeyAndNoArguments_IsTransientAndHasCreatedTime()
    {
        var before = DateTimeOffset.UtcNow;

        var entity = new SampleCreationEntityWithKey();

        Assert.True(entity.IsTransient());
        Assert.InRange(entity.CreatedTime, before, DateTimeOffset.UtcNow);
        Assert.Equal(0L, entity.CreatedId);
        Assert.Null(entity.CreatedBy);
    }

    /// <summary>
    /// 单参构造只写主键，不写创建者
    /// </summary>
    [Fact]
    public void Constructor_WithBasicId_SetsIdAndLeavesCreatorEmpty()
    {
        var entity = new SampleCreationEntityWithKey(11);

        Assert.Equal(11L, entity.BasicId);
        Assert.False(entity.IsTransient());
        Assert.Equal(0L, entity.CreatedId);
        Assert.Null(entity.CreatedBy);
    }

    /// <summary>
    /// 双参构造同时写主键与创建者主键
    /// </summary>
    [Fact]
    public void Constructor_WithBasicIdAndCreatedId_SetsBoth()
    {
        var entity = new SampleCreationEntityWithKey(11, 22);

        Assert.Equal(11L, entity.BasicId);
        Assert.Equal(22L, entity.CreatedId);
    }

    /// <summary>
    /// 创建者名称可写
    /// </summary>
    [Fact]
    public void CreatedBy_WhenAssigned_KeepsAssignedValue()
    {
        var entity = new SampleCreationEntityWithKey(11)
        {
            CreatedBy = "admin"
        };

        Assert.Equal("admin", entity.CreatedBy);
    }

    /// <summary>
    /// 两个版本都实现创建审计接口
    /// </summary>
    [Fact]
    public void CreationEntityBase_ImplementsCreationContracts()
    {
        Assert.IsAssignableFrom<ICreationEntity>(new SampleCreationEntity());
        Assert.IsAssignableFrom<ICreationEntity>(new SampleCreationEntityWithKey());
        Assert.IsAssignableFrom<ICreationEntity<long>>(new SampleCreationEntityWithKey());
        Assert.IsAssignableFrom<IEntityBase<long>>(new SampleCreationEntityWithKey());
    }
}
