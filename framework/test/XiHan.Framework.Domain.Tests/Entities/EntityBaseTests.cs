// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Domain.Tests.Samples;

namespace XiHan.Framework.Domain.Tests.Entities;

/// <summary>
/// 实体基类测试
/// </summary>
/// <remarks>
/// 实体相等性是「同类型 + 同非默认主键」，瞬态实体（主键为默认值）之间永不相等——
/// 这条语义直接决定了实体进 HashSet / 字典后的去重行为，必须锁死。
/// </remarks>
public class EntityBaseTests
{
    /// <summary>
    /// 无主键实体基类的行版本默认为 0 且可写
    /// </summary>
    [Fact]
    public void RowVersion_ByDefault_IsZeroAndWritable()
    {
        var entity = new SampleRowVersionEntity();

        Assert.Equal(0L, entity.RowVersion);

        entity.RowVersion = 9;

        Assert.Equal(9L, entity.RowVersion);
    }

    /// <summary>
    /// 未赋主键的实体是瞬态实体
    /// </summary>
    [Fact]
    public void IsTransient_WhenIdIsDefault_ReturnsTrue()
    {
        var entity = new SampleEntity();

        Assert.True(entity.IsTransient());
        Assert.Equal(0L, entity.BasicId);
    }

    /// <summary>
    /// 通过构造函数赋主键后不再是瞬态实体
    /// </summary>
    [Fact]
    public void IsTransient_WhenIdAssignedByConstructor_ReturnsFalse()
    {
        var entity = new SampleEntity(1);

        Assert.False(entity.IsTransient());
        Assert.Equal(1L, entity.BasicId);
    }

    /// <summary>
    /// 引用类型主键（Guid 空值）同样按默认值判定为瞬态
    /// </summary>
    [Fact]
    public void IsTransient_WhenGuidIdIsEmpty_ReturnsTrue()
    {
        var transient = new SampleGuidEntity();
        var persisted = new SampleGuidEntity(Guid.NewGuid());

        Assert.True(transient.IsTransient());
        Assert.False(persisted.IsTransient());
    }

    /// <summary>
    /// 主键写入后瞬态判定随之翻转
    /// </summary>
    [Fact]
    public void IsTransient_AfterIdAssigned_FlipsToFalse()
    {
        var entity = new SampleEntity();

        Assert.True(entity.IsTransient());

        entity.AssignBasicId(42);

        Assert.False(entity.IsTransient());
    }

    /// <summary>
    /// 同类型同主键的两个实例相等
    /// </summary>
    [Fact]
    public void Equals_WhenSameTypeAndSameId_ReturnsTrue()
    {
        var left = new SampleEntity(7);
        var right = new SampleEntity(7);

        Assert.True(left.Equals(right));
        Assert.True(left == right);
        Assert.False(left != right);
    }

    /// <summary>
    /// 同类型不同主键的两个实例不相等
    /// </summary>
    [Fact]
    public void Equals_WhenSameTypeAndDifferentId_ReturnsFalse()
    {
        var left = new SampleEntity(7);
        var right = new SampleEntity(8);

        Assert.False(left.Equals(right));
        Assert.True(left != right);
    }

    /// <summary>
    /// 主键相同但 CLR 类型不同的实体不相等
    /// </summary>
    [Fact]
    public void Equals_WhenDifferentRuntimeType_ReturnsFalse()
    {
        var left = new SampleEntity(7);
        var right = new AnotherSampleEntity(7);

        Assert.False(left.Equals(right));
    }

    /// <summary>
    /// 派生类型与基类型即使主键相同也不相等
    /// </summary>
    [Fact]
    public void Equals_WhenDerivedTypeAgainstBaseType_ReturnsFalse()
    {
        SampleEntity baseEntity = new(7);
        SampleEntity derivedEntity = new DerivedSampleEntity(7);

        Assert.False(baseEntity.Equals(derivedEntity));
        Assert.False(derivedEntity.Equals(baseEntity));
    }

    /// <summary>
    /// 两个不同的瞬态实体不相等
    /// </summary>
    [Fact]
    public void Equals_WhenBothTransient_ReturnsFalse()
    {
        var left = new SampleEntity();
        var right = new SampleEntity();

        Assert.False(left.Equals(right));
    }

    /// <summary>
    /// 瞬态实体与自身相等，引用判等优先于瞬态判定
    /// </summary>
    [Fact]
    public void Equals_WhenTransientAndSameReference_ReturnsTrue()
    {
        var entity = new SampleEntity();

        Assert.True(entity.Equals(entity));
    }

    /// <summary>
    /// 与 null 比较恒为不等
    /// </summary>
    [Fact]
    public void Equals_WhenOtherIsNull_ReturnsFalse()
    {
        var entity = new SampleEntity(7);

        Assert.False(entity.Equals(null));
        Assert.False(entity.Equals((object?)null));
    }

    /// <summary>
    /// 与非实体对象比较恒为不等
    /// </summary>
    [Fact]
    public void Equals_WhenObjectIsNotEntity_ReturnsFalse()
    {
        var entity = new SampleEntity(7);

        Assert.False(entity.Equals("7"));
    }

    /// <summary>
    /// 两侧均为 null 时相等运算符返回 true
    /// </summary>
    [Fact]
    public void EqualityOperator_WhenBothNull_ReturnsTrue()
    {
        SampleEntity? left = null;
        SampleEntity? right = null;

        Assert.True(left == right);
        Assert.False(left != right);
    }

    /// <summary>
    /// 仅一侧为 null 时相等运算符返回 false
    /// </summary>
    [Fact]
    public void EqualityOperator_WhenOnlyOneSideIsNull_ReturnsFalse()
    {
        SampleEntity? left = null;
        var right = new SampleEntity(7);

        Assert.False(left == right);
        Assert.True(left != right);
        Assert.False(right == left);
    }

    /// <summary>
    /// 相等的实体必须有相同哈希码
    /// </summary>
    [Fact]
    public void GetHashCode_WhenEntitiesAreEqual_ReturnsSameValue()
    {
        var left = new SampleEntity(7);
        var right = new SampleEntity(7);

        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    /// <summary>
    /// 瞬态实体退化为引用哈希，不同实例哈希彼此独立
    /// </summary>
    [Fact]
    public void GetHashCode_WhenTransient_IsStablePerInstance()
    {
        var entity = new SampleEntity();

        // 瞬态实体走 object.GetHashCode()，同实例多次调用必须稳定
        Assert.Equal(entity.GetHashCode(), entity.GetHashCode());
    }

    /// <summary>
    /// 相同主键的实体在哈希集合中被视为同一元素
    /// </summary>
    [Fact]
    public void HashSet_WithSameIdEntities_KeepsSingleElement()
    {
        var set = new HashSet<SampleEntity>
        {
            new(7),
            new(7),
            new(8)
        };

        Assert.Equal(2, set.Count);
    }

    /// <summary>
    /// 瞬态实体在哈希集合中互不去重
    /// </summary>
    [Fact]
    public void HashSet_WithTransientEntities_KeepsEveryInstance()
    {
        var set = new HashSet<SampleEntity>
        {
            new(),
            new(),
            new()
        };

        Assert.Equal(3, set.Count);
    }

    /// <summary>
    /// 泛型实体基类同时暴露行版本
    /// </summary>
    [Fact]
    public void RowVersion_OnGenericEntity_DefaultsToZero()
    {
        var entity = new SampleEntity(7);

        Assert.Equal(0L, entity.RowVersion);
        Assert.IsAssignableFrom<EntityBase>(entity);
    }
}
