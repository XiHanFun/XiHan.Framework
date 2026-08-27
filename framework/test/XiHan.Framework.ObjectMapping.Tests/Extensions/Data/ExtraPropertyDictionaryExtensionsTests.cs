// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.ObjectMapping.Extensions.Data;
using XiHan.Framework.ObjectMapping.Tests.Fakes;

namespace XiHan.Framework.ObjectMapping.Tests.Extensions.Data;

/// <summary>
/// 额外属性字典扩展方法测试
/// </summary>
/// <remarks>
/// ToEnum 有一个容易被忽略的副作用：解析成功后会把字典里的原值「就地替换」成枚举值，
/// 所以除了返回值，还必须断言字典本身被改写——反序列化后重复取值就是靠这个变成 O(1) 的。
/// HasSameItems 的比较口径是 ToString()，因此 1 与 "1" 会被判为相同，这一点也必须钉死。
/// </remarks>
public class ExtraPropertyDictionaryExtensionsTests
{
    /// <summary>
    /// 值已经是目标枚举类型时原样返回
    /// </summary>
    [Fact]
    public void ToEnumGeneric_WhenValueAlreadyEnum_ReturnsSameValue()
    {
        var sut = new ExtraPropertyDictionary
        {
            ["Kind"] = FakeExtensionEnum.Second
        };

        var result = sut.ToEnum<FakeExtensionEnum>("Kind");

        Assert.Equal(FakeExtensionEnum.Second, result);
    }

    /// <summary>
    /// 值为枚举名称字符串时解析成功，并把字典里的值替换为枚举
    /// </summary>
    [Fact]
    public void ToEnumGeneric_WhenValueIsName_ParsesAndRewritesStoredValue()
    {
        var sut = new ExtraPropertyDictionary
        {
            ["Kind"] = "Second"
        };

        var result = sut.ToEnum<FakeExtensionEnum>("Kind");

        Assert.Equal(FakeExtensionEnum.Second, result);
        Assert.Equal(FakeExtensionEnum.Second, sut["Kind"]);
    }

    /// <summary>
    /// 名称大小写不敏感
    /// </summary>
    [Theory]
    [InlineData("second")]
    [InlineData("SECOND")]
    [InlineData("Second")]
    public void ToEnumGeneric_WhenNameCaseDiffers_StillParses(string storedName)
    {
        var sut = new ExtraPropertyDictionary
        {
            ["Kind"] = storedName
        };

        Assert.Equal(FakeExtensionEnum.Second, sut.ToEnum<FakeExtensionEnum>("Kind"));
    }

    /// <summary>
    /// 值为数字（或数字字符串）时按枚举基础值解析
    /// </summary>
    [Fact]
    public void ToEnumGeneric_WhenValueIsNumeric_ParsesByUnderlyingValue()
    {
        var fromInt = new ExtraPropertyDictionary
        {
            ["Kind"] = 1
        };
        var fromText = new ExtraPropertyDictionary
        {
            ["Kind"] = "2"
        };

        Assert.Equal(FakeExtensionEnum.First, fromInt.ToEnum<FakeExtensionEnum>("Kind"));
        Assert.Equal(FakeExtensionEnum.Second, fromText.ToEnum<FakeExtensionEnum>("Kind"));
    }

    /// <summary>
    /// 值不是合法枚举名称时抛出参数异常
    /// </summary>
    [Fact]
    public void ToEnumGeneric_WhenValueNotParsable_ThrowsArgumentException()
    {
        var sut = new ExtraPropertyDictionary
        {
            ["Kind"] = "不存在的项"
        };

        Assert.ThrowsAny<ArgumentException>(() => sut.ToEnum<FakeExtensionEnum>("Kind"));
    }

    /// <summary>
    /// 键不存在时按字典索引器语义抛 KeyNotFoundException，而不是返回默认枚举值
    /// </summary>
    [Fact]
    public void ToEnumGeneric_WhenKeyMissing_ThrowsKeyNotFoundException()
    {
        var sut = new ExtraPropertyDictionary();

        Assert.Throws<KeyNotFoundException>(() => sut.ToEnum<FakeExtensionEnum>("Kind"));
    }

    /// <summary>
    /// 字典为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void ToEnumGeneric_WhenDictionaryNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => ExtraPropertyDictionaryExtensions.ToEnum<FakeExtensionEnum>(null!, "Kind"));

        Assert.Equal("extraPropertyDictionary", exception.ParamName);
    }

    /// <summary>
    /// 键为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void ToEnumGeneric_WhenKeyNull_ThrowsArgumentNullException()
    {
        var sut = new ExtraPropertyDictionary();

        var exception = Assert.Throws<ArgumentNullException>(() => sut.ToEnum<FakeExtensionEnum>(null!));

        Assert.Equal("key", exception.ParamName);
    }

    /// <summary>
    /// 键为空白时抛出 ArgumentException
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ToEnumGeneric_WhenKeyBlank_ThrowsArgumentException(string key)
    {
        var sut = new ExtraPropertyDictionary();

        var exception = Assert.Throws<ArgumentException>(() => sut.ToEnum<FakeExtensionEnum>(key));

        Assert.Equal("key", exception.ParamName);
    }

    /// <summary>
    /// 非泛型重载在目标类型是枚举时同样会解析并改写字典
    /// </summary>
    [Fact]
    public void ToEnum_WhenEnumTypeGiven_ParsesAndRewritesStoredValue()
    {
        var sut = new ExtraPropertyDictionary
        {
            ["Kind"] = "First"
        };

        var result = sut.ToEnum("Kind", typeof(FakeExtensionEnum));

        Assert.Equal(FakeExtensionEnum.First, result);
        Assert.Equal(FakeExtensionEnum.First, sut["Kind"]);
    }

    /// <summary>
    /// 非泛型重载在目标类型不是枚举时直接返回原值，既不解析也不改写
    /// </summary>
    [Fact]
    public void ToEnum_WhenTypeIsNotEnum_ReturnsRawValueUnchanged()
    {
        var sut = new ExtraPropertyDictionary
        {
            ["Kind"] = "First"
        };

        var result = sut.ToEnum("Kind", typeof(int));

        Assert.Equal("First", result);
        Assert.Equal("First", sut["Kind"]);
    }

    /// <summary>
    /// 非泛型重载在值已经是目标枚举类型时原样返回
    /// </summary>
    [Fact]
    public void ToEnum_WhenValueAlreadyEnum_ReturnsSameValue()
    {
        var sut = new ExtraPropertyDictionary
        {
            ["Kind"] = FakeExtensionEnum.Second
        };

        Assert.Equal(FakeExtensionEnum.Second, sut.ToEnum("Kind", typeof(FakeExtensionEnum)));
    }

    /// <summary>
    /// 非泛型重载的枚举类型为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void ToEnum_WhenEnumTypeNull_ThrowsArgumentNullException()
    {
        var sut = new ExtraPropertyDictionary
        {
            ["Kind"] = "First"
        };

        var exception = Assert.Throws<ArgumentNullException>(() => sut.ToEnum("Kind", null!));

        Assert.Equal("enumType", exception.ParamName);
    }

    /// <summary>
    /// 两个空字典视为内容相同
    /// </summary>
    [Fact]
    public void HasSameItems_WhenBothEmpty_ReturnsTrue()
    {
        Assert.True(new ExtraPropertyDictionary().HasSameItems(new ExtraPropertyDictionary()));
    }

    /// <summary>
    /// 键值完全一致时返回 true
    /// </summary>
    [Fact]
    public void HasSameItems_WhenSameKeysAndValues_ReturnsTrue()
    {
        var left = new ExtraPropertyDictionary
        {
            ["Name"] = "曦寒",
            ["Age"] = 18
        };
        var right = new ExtraPropertyDictionary
        {
            ["Age"] = 18,
            ["Name"] = "曦寒"
        };

        Assert.True(left.HasSameItems(right));
    }

    /// <summary>
    /// 条目数量不同时返回 false
    /// </summary>
    [Fact]
    public void HasSameItems_WhenCountDiffers_ReturnsFalse()
    {
        var left = new ExtraPropertyDictionary
        {
            ["Name"] = "曦寒"
        };
        var right = new ExtraPropertyDictionary
        {
            ["Name"] = "曦寒",
            ["Age"] = 18
        };

        Assert.False(left.HasSameItems(right));
        Assert.False(right.HasSameItems(left));
    }

    /// <summary>
    /// 数量相同但键不同时返回 false
    /// </summary>
    [Fact]
    public void HasSameItems_WhenKeyDiffers_ReturnsFalse()
    {
        var left = new ExtraPropertyDictionary
        {
            ["Name"] = "曦寒"
        };
        var right = new ExtraPropertyDictionary
        {
            ["Nickname"] = "曦寒"
        };

        Assert.False(left.HasSameItems(right));
    }

    /// <summary>
    /// 比较口径是 ToString()，因此装箱整数 1 与字符串 "1" 被判为相同
    /// </summary>
    [Fact]
    public void HasSameItems_ComparesByStringRepresentation()
    {
        var left = new ExtraPropertyDictionary
        {
            ["Age"] = 1
        };
        var right = new ExtraPropertyDictionary
        {
            ["Age"] = "1"
        };

        Assert.True(left.HasSameItems(right));
    }

    /// <summary>
    /// 两侧同一个键都存 null 时视为相同
    /// </summary>
    [Fact]
    public void HasSameItems_WhenBothValuesNull_ReturnsTrue()
    {
        var left = new ExtraPropertyDictionary
        {
            ["Name"] = null
        };
        var right = new ExtraPropertyDictionary
        {
            ["Name"] = null
        };

        Assert.True(left.HasSameItems(right));
    }

    /// <summary>
    /// null 与空串不等价，不能被 ToString 口径抹平
    /// </summary>
    [Fact]
    public void HasSameItems_WhenNullVersusEmptyString_ReturnsFalse()
    {
        var left = new ExtraPropertyDictionary
        {
            ["Name"] = null
        };
        var right = new ExtraPropertyDictionary
        {
            ["Name"] = string.Empty
        };

        Assert.False(left.HasSameItems(right));
    }

    /// <summary>
    /// 任一字典为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void HasSameItems_WhenAnyDictionaryNull_ThrowsArgumentNullException()
    {
        var sut = new ExtraPropertyDictionary();

        var first = Assert.Throws<ArgumentNullException>(
            () => ExtraPropertyDictionaryExtensions.HasSameItems(null!, sut));
        var second = Assert.Throws<ArgumentNullException>(() => sut.HasSameItems(null!));

        Assert.Equal("dictionary", first.ParamName);
        Assert.Equal("otherDictionary", second.ParamName);
    }
}
