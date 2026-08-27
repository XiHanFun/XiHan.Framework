// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Utils.Tests.Extensions;

/// <summary>
/// IComparable 扩展方法测试
/// </summary>
public class ComparableExtensionsTests
{
    /// <summary>
    /// 闭区间范围判断包含两端
    /// </summary>
    [Theory]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(10, true)]
    [InlineData(0, false)]
    [InlineData(11, false)]
    public void IsInRange_WithInclusiveBounds_IncludesEndpoints(int value, bool expected)
    {
        Assert.Equal(expected, value.IsInRange(1, 10));
    }

    /// <summary>
    /// 最小值大于最大值时抛参数异常
    /// </summary>
    [Fact]
    public void IsInRange_WhenMinGreaterThanMax_Throws()
    {
        var value = 5;

        Assert.Throws<ArgumentException>(() => value.IsInRange(10, 1));
    }

    /// <summary>
    /// 自定义边界包含性时端点按开关取舍
    /// </summary>
    [Fact]
    public void IsInRange_WithCustomBoundaryFlags_RespectsFlags()
    {
        var min = 1;
        var max = 10;

        Assert.False(min.IsInRange(1, 10, false, true));
        Assert.True(min.IsInRange(1, 10, true, true));
        Assert.False(max.IsInRange(1, 10, true, false));
        Assert.True(max.IsInRange(1, 10, true, true));
    }

    /// <summary>
    /// 开区间范围判断排除两端
    /// </summary>
    [Theory]
    [InlineData(1, false)]
    [InlineData(5, true)]
    [InlineData(10, false)]
    public void IsBetween_WithExclusiveBounds_ExcludesEndpoints(int value, bool expected)
    {
        Assert.Equal(expected, value.IsBetween(1, 10));
    }

    /// <summary>
    /// 最小值不小于最大值时抛参数异常
    /// </summary>
    [Fact]
    public void IsBetween_WhenMinNotLessThanMax_Throws()
    {
        var value = 5;

        Assert.Throws<ArgumentException>(() => value.IsBetween(10, 10));
    }

    /// <summary>
    /// 大小比较系列方法语义正确
    /// </summary>
    [Fact]
    public void ComparisonHelpers_ReturnExpectedResults()
    {
        var value = 5;

        Assert.True(value.IsGreaterThan(4));
        Assert.False(value.IsGreaterThan(5));
        Assert.True(value.IsGreaterThanOrEqual(5));
        Assert.True(value.IsLessThan(6));
        Assert.False(value.IsLessThan(5));
        Assert.True(value.IsLessThanOrEqual(5));
        Assert.True(value.IsEqualTo(5));
        Assert.False(value.IsEqualTo(6));
        Assert.True(value.IsNotEqualTo(6));
        Assert.False(value.IsNotEqualTo(5));
    }

    /// <summary>
    /// 限制到区间内，越界时贴边
    /// </summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(2, 2)]
    [InlineData(9, 3)]
    public void Clamp_ClipsValueIntoRange(int value, int expected)
    {
        Assert.Equal(expected, value.Clamp(1, 3));
    }

    /// <summary>
    /// 最小值大于最大值时抛参数异常
    /// </summary>
    [Fact]
    public void Clamp_WhenMinGreaterThanMax_Throws()
    {
        var value = 2;

        Assert.Throws<ArgumentException>(() => value.Clamp(3, 1));
    }

    /// <summary>
    /// 下限与上限保护
    /// </summary>
    [Fact]
    public void AtLeastAndAtMost_ApplyOneSidedBounds()
    {
        var small = 1;
        var large = 9;

        Assert.Equal(5, small.AtLeast(5));
        Assert.Equal(9, large.AtLeast(5));
        Assert.Equal(1, small.AtMost(5));
        Assert.Equal(5, large.AtMost(5));
    }

    /// <summary>
    /// 两值取大取小
    /// </summary>
    [Fact]
    public void MaxAndMin_WithTwoValues_ReturnExpected()
    {
        var value = 3;

        Assert.Equal(7, value.Max(7));
        Assert.Equal(3, value.Max(1));
        Assert.Equal(1, value.Min(1));
        Assert.Equal(3, value.Min(7));
    }

    /// <summary>
    /// 多值取大取小
    /// </summary>
    [Fact]
    public void MaxAndMin_WithParams_ScanAllValues()
    {
        var value = 3;

        Assert.Equal(9, value.Max(1, 9, 5));
        Assert.Equal(1, value.Min(1, 9, 5));
        Assert.Equal(3, value.Max(1, 2));
        Assert.Equal(3, value.Min(4, 5));
    }

    /// <summary>
    /// 参数数组为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void MaxAndMin_WhenParamsArrayIsNull_Throws()
    {
        var value = 3;

        Assert.Throws<ArgumentNullException>(() => value.Max((int[])null!));
        Assert.Throws<ArgumentNullException>(() => value.Min((int[])null!));
    }

    /// <summary>
    /// 引用类型的空值安全比较把 null 当作最小值
    /// </summary>
    [Fact]
    public void CompareToNullSafe_OnReferenceType_TreatsNullAsSmallest()
    {
        string? nothing = null;
        string? something = "a";

        Assert.Equal(0, nothing.CompareToNullSafe(null));
        Assert.Equal(-1, nothing.CompareToNullSafe(something));
        Assert.Equal(1, something.CompareToNullSafe(null));
        Assert.True(something.CompareToNullSafe("b") < 0);
    }

    /// <summary>
    /// 可空值类型的空值安全比较把 null 当作最小值
    /// </summary>
    [Fact]
    public void CompareToNullSafe_OnNullableValueType_TreatsNullAsSmallest()
    {
        int? nothing = null;
        int? five = 5;

        Assert.Equal(0, nothing.CompareToNullSafe(null));
        Assert.Equal(-1, nothing.CompareToNullSafe(five));
        Assert.Equal(1, five.CompareToNullSafe(null));
        Assert.True(five.CompareToNullSafe(9) < 0);
    }

    /// <summary>
    /// 空值安全取大取小，全为 null 时返回 null
    /// </summary>
    [Fact]
    public void MaxNullSafeAndMinNullSafe_HandleNulls()
    {
        string? nothing = null;
        string? a = "a";
        string? b = "b";

        Assert.Equal("b", a.MaxNullSafe(b));
        Assert.Equal("a", a.MinNullSafe(b));
        Assert.Equal("a", nothing.MaxNullSafe(a));
        Assert.Equal("a", nothing.MinNullSafe(a));
        Assert.Equal("a", a.MaxNullSafe(null));
        Assert.Null(nothing.MaxNullSafe(null));
        Assert.Null(nothing.MinNullSafe(null));
    }

    /// <summary>
    /// 判断值是否落在集合或参数列表中
    /// </summary>
    [Fact]
    public void IsInAndIsNotIn_CheckMembership()
    {
        var value = 3;
        IEnumerable<int> collection = new[] { 1, 2, 3 };

        Assert.True(value.IsIn(collection));
        Assert.True(value.IsIn(1, 2, 3));
        Assert.False(value.IsIn(4, 5));
        Assert.True(value.IsNotIn(4, 5));
        Assert.False(value.IsNotIn(collection));
    }

    /// <summary>
    /// 集合为空时判定为不在其中
    /// </summary>
    [Fact]
    public void IsIn_WhenCollectionEmpty_ReturnsFalse()
    {
        var value = 3;
        IEnumerable<int> empty = Array.Empty<int>();

        Assert.False(value.IsIn(empty));
    }

    /// <summary>
    /// 集合为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void IsIn_WhenCollectionIsNull_Throws()
    {
        var value = 3;

        Assert.Throws<ArgumentNullException>(() => value.IsIn((IEnumerable<int>)null!));
        Assert.Throws<ArgumentNullException>(() => value.IsIn((int[])null!));
    }

    /// <summary>
    /// 绝对比较结果永远非负
    /// </summary>
    [Fact]
    public void AbsCompareTo_IsAlwaysNonNegative()
    {
        var small = 1;
        var large = 9;

        Assert.Equal(1, small.AbsCompareTo(large));
        Assert.Equal(1, large.AbsCompareTo(small));
        Assert.Equal(0, small.AbsCompareTo(1));
    }

    /// <summary>
    /// 默认值判断针对值类型的零值
    /// </summary>
    [Fact]
    public void IsDefaultAndIsNotDefault_DetectValueTypeDefault()
    {
        var zero = 0;
        var one = 1;

        Assert.True(zero.IsDefault());
        Assert.False(zero.IsNotDefault());
        Assert.False(one.IsDefault());
        Assert.True(one.IsNotDefault());
    }

    /// <summary>
    /// 条件成立返回原值，否则返回替代值
    /// </summary>
    [Fact]
    public void IfThen_WithAlternativeValue_PicksByPredicate()
    {
        var big = 9;
        var small = 1;

        Assert.Equal(9, big.IfThen(x => x > 3, 0));
        Assert.Equal(0, small.IfThen(x => x > 3, 0));
    }

    /// <summary>
    /// 条件不成立时才调用替代值工厂
    /// </summary>
    [Fact]
    public void IfThen_WithFactory_InvokesFactoryOnlyWhenNeeded()
    {
        var big = 9;
        var small = 1;
        var calls = 0;

        var kept = big.IfThen(x => x > 3, () =>
        {
            calls++;
            return 0;
        });
        var replaced = small.IfThen(x => x > 3, () =>
        {
            calls++;
            return -1;
        });

        Assert.Equal(9, kept);
        Assert.Equal(-1, replaced);
        Assert.Equal(1, calls);
    }

    /// <summary>
    /// 谓词为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void IfThen_WhenPredicateIsNull_Throws()
    {
        var value = 1;

        Assert.Throws<ArgumentNullException>(() => value.IfThen(null!, 0));
    }
}
