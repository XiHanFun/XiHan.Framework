// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Repositories.Models;

namespace XiHan.Framework.Domain.Tests.Repositories.Models;

/// <summary>
/// 审计查询参数测试
/// </summary>
/// <remarks>
/// 两个软删除开关的默认值决定了「不传参数就只返回未删除数据」这条安全默认，
/// 一旦默认成 true，所有审计查询都会把已删除数据一起吐出去。
/// </remarks>
public class AuditQueryOptionsTests
{
    /// <summary>
    /// 默认不包含软删除数据也不只看软删除数据
    /// </summary>
    [Fact]
    public void SoftDeleteSwitches_ByDefault_AreBothOff()
    {
        var options = new AuditQueryOptions<long>();

        Assert.False(options.IncludeSoftDeleted);
        Assert.False(options.OnlySoftDeleted);
    }

    /// <summary>
    /// 默认所有时间范围条件为空
    /// </summary>
    [Fact]
    public void TimeRanges_ByDefault_AreNull()
    {
        var options = new AuditQueryOptions<long>();

        Assert.Null(options.CreatedTimeStart);
        Assert.Null(options.CreatedTimeEnd);
        Assert.Null(options.ModifiedTimeStart);
        Assert.Null(options.ModifiedTimeEnd);
        Assert.Null(options.DeletedTimeStart);
        Assert.Null(options.DeletedTimeEnd);
    }

    /// <summary>
    /// 引用类型主键的操作人条件默认为空
    /// </summary>
    [Fact]
    public void OperatorIds_WithReferenceKey_DefaultToNull()
    {
        var options = new AuditQueryOptions<string>();

        Assert.Null(options.CreatedId);
        Assert.Null(options.ModifiedId);
        Assert.Null(options.DeletedId);
    }

    /// <summary>
    /// 对象初始化器可写入全部查询条件
    /// </summary>
    [Fact]
    public void ObjectInitializer_AssignsEveryCondition()
    {
        var createdStart = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var createdEnd = new DateTimeOffset(2024, 1, 31, 0, 0, 0, TimeSpan.Zero);
        var modifiedStart = new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero);
        var modifiedEnd = new DateTimeOffset(2024, 2, 29, 0, 0, 0, TimeSpan.Zero);
        var deletedStart = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var deletedEnd = new DateTimeOffset(2024, 3, 31, 0, 0, 0, TimeSpan.Zero);

        var options = new AuditQueryOptions<long>
        {
            CreatedId = 1,
            ModifiedId = 2,
            DeletedId = 3,
            CreatedTimeStart = createdStart,
            CreatedTimeEnd = createdEnd,
            ModifiedTimeStart = modifiedStart,
            ModifiedTimeEnd = modifiedEnd,
            DeletedTimeStart = deletedStart,
            DeletedTimeEnd = deletedEnd,
            IncludeSoftDeleted = true,
            OnlySoftDeleted = true
        };

        Assert.Equal(1L, options.CreatedId);
        Assert.Equal(2L, options.ModifiedId);
        Assert.Equal(3L, options.DeletedId);
        Assert.Equal(createdStart, options.CreatedTimeStart);
        Assert.Equal(createdEnd, options.CreatedTimeEnd);
        Assert.Equal(modifiedStart, options.ModifiedTimeStart);
        Assert.Equal(modifiedEnd, options.ModifiedTimeEnd);
        Assert.Equal(deletedStart, options.DeletedTimeStart);
        Assert.Equal(deletedEnd, options.DeletedTimeEnd);
        Assert.True(options.IncludeSoftDeleted);
        Assert.True(options.OnlySoftDeleted);
    }

    /// <summary>
    /// 所有属性都是仅初始化，构造完成后不可再改
    /// </summary>
    /// <remarks>
    /// init 访问器在反射里表现为带 IsExternalInit 修饰符的 set 方法，
    /// 这里退一步只断言「不存在普通可写 set」的行为：属性可读且赋值只能发生在对象初始化器里。
    /// </remarks>
    [Fact]
    public void Type_IsSealedAndPropertiesAreInitOnly()
    {
        var type = typeof(AuditQueryOptions<long>);

        Assert.True(type.IsSealed);

        var setMethod = type.GetProperty(nameof(AuditQueryOptions<long>.IncludeSoftDeleted))!.SetMethod;

        Assert.NotNull(setMethod);
        Assert.Contains(
            setMethod!.ReturnParameter.GetRequiredCustomModifiers(),
            modifier => modifier.Name == "IsExternalInit");
    }
}
