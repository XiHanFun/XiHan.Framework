// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Utils.Tests.Extensions;

/// <summary>
/// 类型转换扩展方法测试
/// </summary>
/// <remarks>
/// 这层是对 ConvertHelper 的门面，重点验证"成功路径取到值、失败路径回落默认值"这条契约，
/// 不重复验证底层每种数字类型的解析细节。
/// </remarks>
public class ConverterExtensionsTests
{
    /// <summary>
    /// 泛型转换成功时返回目标类型的值
    /// </summary>
    [Fact]
    public void ConvertTo_WhenConvertible_ReturnsConvertedValue()
    {
        object? text = "123";
        object? number = 123;

        Assert.Equal(123, text.ConvertTo<int>());
        Assert.Equal("123", number.ConvertTo<string>());
    }

    /// <summary>
    /// 泛型转换失败或入参为 null 时回落默认值
    /// </summary>
    [Fact]
    public void ConvertTo_WhenNotConvertible_ReturnsDefaultValue()
    {
        object? invalid = "abc";
        object? nothing = null;

        Assert.Equal(-1, invalid.ConvertTo(-1));
        Assert.Equal(5, nothing.ConvertTo(5));
    }

    /// <summary>
    /// 尝试转换成功时返回真并给出结果
    /// </summary>
    [Fact]
    public void TryConvertTo_ReportsSuccessAndFailure()
    {
        object? text = "12";
        object? invalid = "abc";

        Assert.True(text.TryConvertTo<int>(out var parsed));
        Assert.Equal(12, parsed);
        Assert.False(invalid.TryConvertTo<int>(out var failed));
        Assert.Equal(0, failed);
    }

    /// <summary>
    /// 布尔转换识别常见字符串写法
    /// </summary>
    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("1", true)]
    [InlineData("yes", true)]
    [InlineData("y", true)]
    [InlineData("on", true)]
    [InlineData("false", false)]
    [InlineData("0", false)]
    [InlineData("no", false)]
    [InlineData("off", false)]
    public void ConvertToBool_RecognizesCommonStrings(string value, bool expected)
    {
        object? boxed = value;

        Assert.Equal(expected, boxed.ConvertToBool());
    }

    /// <summary>
    /// 无法识别的布尔字符串与 null 回落默认值
    /// </summary>
    [Fact]
    public void ConvertToBool_WhenUnrecognized_ReturnsDefaultValue()
    {
        object? unknown = "maybe";
        object? nothing = null;

        Assert.True(unknown.ConvertToBool(true));
        Assert.False(unknown.ConvertToBool(false));
        Assert.True(nothing.ConvertToBool(true));
    }

    /// <summary>
    /// 整数系列转换在可解析时取值，不可解析时回落默认值
    /// </summary>
    [Fact]
    public void IntegerConversions_ParseOrFallBack()
    {
        object? valid = "42";
        object? invalid = "abc";

        Assert.Equal((byte)42, valid.ConvertToByte());
        Assert.Equal((sbyte)42, valid.ConvertToSByte());
        Assert.Equal((short)42, valid.ConvertToShort());
        Assert.Equal((ushort)42, valid.ConvertToUShort());
        Assert.Equal(42, valid.ConvertToInt());
        Assert.Equal(42u, valid.ConvertToUInt());
        Assert.Equal(42L, valid.ConvertToLong());
        Assert.Equal(42UL, valid.ConvertToULong());

        Assert.Equal((byte)7, invalid.ConvertToByte(7));
        Assert.Equal(7, invalid.ConvertToInt(7));
        Assert.Equal(7L, invalid.ConvertToLong(7));
    }

    /// <summary>
    /// 浮点与十进制转换按不变文化解析
    /// </summary>
    [Fact]
    public void FloatingConversions_UseInvariantCulture()
    {
        object? valid = "1.5";
        object? invalid = "abc";

        Assert.Equal(1.5f, valid.ConvertToFloat());
        Assert.Equal(1.5d, valid.ConvertToDouble());
        Assert.Equal(1.5m, valid.ConvertToDecimal());
        Assert.Equal(2.5d, invalid.ConvertToDouble(2.5d));
        Assert.Equal(2.5m, invalid.ConvertToDecimal(2.5m));
    }

    /// <summary>
    /// 日期转换识别标准写法，失败时回落默认值
    /// </summary>
    [Fact]
    public void ConvertToDateTime_ParsesIsoLikeStringOrFallsBack()
    {
        object? valid = "2024-01-02";
        object? invalid = "not-a-date";
        var fallback = new DateTime(1999, 12, 31);

        Assert.Equal(new DateTime(2024, 1, 2), valid.ConvertToDateTime());
        Assert.Equal(fallback, invalid.ConvertToDateTime(fallback));
    }

    /// <summary>
    /// 带时区日期由 DateTime 直接包装
    /// </summary>
    [Fact]
    public void ConvertToDateTimeOffset_WrapsDateTime()
    {
        var source = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Unspecified);
        object? boxed = source;

        var result = boxed.ConvertToDateTimeOffset();

        Assert.Equal(source, result.DateTime);
    }

    /// <summary>
    /// GUID 转换识别标准写法，失败时回落默认值
    /// </summary>
    [Fact]
    public void ConvertToGuid_ParsesOrFallsBack()
    {
        var expected = Guid.NewGuid();
        object? valid = expected.ToString();
        object? invalid = "not-a-guid";

        Assert.Equal(expected, valid.ConvertToGuid());
        Assert.Equal(Guid.Empty, invalid.ConvertToGuid());
    }

    /// <summary>
    /// 枚举转换支持名称与底层数值，失败时回落默认值
    /// </summary>
    [Fact]
    public void ConvertToEnum_SupportsNameAndNumericValue()
    {
        object? name = "Monday";
        object? number = 1;
        object? invalid = "NotADay";
        object? nothing = null;

        Assert.Equal(DayOfWeek.Monday, name.ConvertToEnum<DayOfWeek>());
        Assert.Equal(DayOfWeek.Monday, number.ConvertToEnum<DayOfWeek>());
        Assert.Equal(DayOfWeek.Sunday, invalid.ConvertToEnum(DayOfWeek.Sunday));
        Assert.Equal(DayOfWeek.Friday, nothing.ConvertToEnum(DayOfWeek.Friday));
    }

    /// <summary>
    /// 序列转数组时逐元素转换
    /// </summary>
    [Fact]
    public void ConvertToArray_ConvertsEachElement()
    {
        object? source = new[] { "1", "2", "3" };

        Assert.Equal(new[] { 1, 2, 3 }, source.ConvertToArray<int>());
    }

    /// <summary>
    /// null 转数组得到空数组，单值转数组得到单元素数组
    /// </summary>
    [Fact]
    public void ConvertToArray_HandlesNullAndSingleValue()
    {
        object? nothing = null;
        object? single = 5;

        Assert.Empty(nothing.ConvertToArray<int>());
        Assert.Equal(new[] { 5 }, single.ConvertToArray<int>());
    }

    /// <summary>
    /// 序列转列表保持元素顺序
    /// </summary>
    [Fact]
    public void ConvertToList_KeepsOrder()
    {
        object? source = new[] { "a", "b" };

        Assert.Equal(new[] { "a", "b" }, source.ConvertToList<string>());
    }

    /// <summary>
    /// 安全转换在底层抛异常时也只回落默认值
    /// </summary>
    [Fact]
    public void SafeConversions_NeverThrow()
    {
        object? invalid = "abc";

        Assert.True(invalid.ConvertToBoolSafe(true));
        Assert.Equal((byte)1, invalid.ConvertToByteSafe(1));
        Assert.Equal((sbyte)1, invalid.ConvertToSByteSafe(1));
        Assert.Equal((short)1, invalid.ConvertToShortSafe(1));
        Assert.Equal((ushort)1, invalid.ConvertToUShortSafe(1));
        Assert.Equal(1, invalid.ConvertToIntSafe(1));
        Assert.Equal(1u, invalid.ConvertToUIntSafe(1));
        Assert.Equal(1L, invalid.ConvertToLongSafe(1));
        Assert.Equal(1UL, invalid.ConvertToULongSafe(1));
        Assert.Equal(1f, invalid.ConvertToFloatSafe(1));
        Assert.Equal(1d, invalid.ConvertToDoubleSafe(1));
        Assert.Equal(1m, invalid.ConvertToDecimalSafe(1));
        Assert.Equal(Guid.Empty, invalid.ConvertToGuidSafe());
    }

    /// <summary>
    /// 安全日期转换在失败时回落默认值
    /// </summary>
    [Fact]
    public void SafeDateConversions_FallBackOnFailure()
    {
        object? invalid = "abc";
        var fallback = new DateTime(2000, 1, 1);

        Assert.Equal(fallback, invalid.ConvertToDateTimeSafe(fallback));
        Assert.Equal(default(DateTimeOffset), invalid.ConvertToDateTimeOffsetSafe());
    }

    /// <summary>
    /// 可格式化对象按格式串与文化输出
    /// </summary>
    [Fact]
    public void ConvertToFormattedString_UsesFormatAndProvider()
    {
        object? value = 1234.5678d;

        Assert.Equal("1234.57", value.ConvertToFormattedString("F2", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 非可格式化对象退化为 ToString，null 得到空串
    /// </summary>
    [Fact]
    public void ConvertToFormattedString_HandlesNonFormattableAndNull()
    {
        object? plain = "text";
        object? nothing = null;

        Assert.Equal("text", plain.ConvertToFormattedString());
        Assert.Equal(string.Empty, nothing.ConvertToFormattedString());
    }

    /// <summary>
    /// 不变文化输出不受当前文化影响
    /// </summary>
    [Fact]
    public void ConvertToInvariantString_UsesInvariantCulture()
    {
        object? value = 1.5d;

        Assert.Equal("1.5", value.ConvertToInvariantString());
    }

    /// <summary>
    /// null 转非空字符串时使用默认值
    /// </summary>
    [Fact]
    public void ConvertToNonNullString_FallsBackForNull()
    {
        object? nothing = null;
        object? value = 12;

        Assert.Equal("默认", nothing.ConvertToNonNullString("默认"));
        Assert.Equal(string.Empty, nothing.ConvertToNonNullString());
        Assert.Equal("12", value.ConvertToNonNullString());
    }

    /// <summary>
    /// 可空转换在 null 或失败时得到 null
    /// </summary>
    [Fact]
    public void ConvertToNullable_ReturnsNullOnFailure()
    {
        object? valid = "5";
        object? invalid = "abc";
        object? nothing = null;

        Assert.Equal(5, valid.ConvertToNullable<int>());
        Assert.Null(invalid.ConvertToNullable<int>());
        Assert.Null(nothing.ConvertToNullable<int>());
    }
}
