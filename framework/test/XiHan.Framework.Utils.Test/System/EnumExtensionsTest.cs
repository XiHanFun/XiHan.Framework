#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EnumExtensionsTest
// Guid:06cb9fee-0e37-4df5-8ced-7a7b2d5bb6b7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/24 2:24:12
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel;
using System.Reflection;
using XiHan.Framework.Utils.Enums;

namespace XiHan.Framework.Utils.Test.System;

/// <summary>
/// 枚举扩展方法测试
/// </summary>
public class EnumExtensionsTest
{
    /// <summary>
    /// 测试用枚举
    /// </summary>
    private enum TestEnum
    {
        /// <summary>
        /// 值1
        /// </summary>
        [Description("描述1")]
        Value1 = 1,

        /// <summary>
        /// 值2
        /// </summary>
        [Description("描述2")]
        Value2 = 2,

        /// <summary>
        /// 值3
        /// </summary>
        [Description("描述3")]
        Value3 = 3
    }

    #region 获取枚举值测试

    [Fact]
    public void GetValue_WithEnumObject_ReturnsCorrectValue()
    {
        // Arrange
        var enumValue = TestEnum.Value1;

        // Act
        var result = enumValue.GetValue();

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public void GetValue_WithTypeAndName_ReturnsCorrectValue()
    {
        // Arrange
        var enumType = typeof(TestEnum);
        var enumName = "Value2";

        // Act
        var result = EnumExtensions.GetValue(enumType, enumName);

        // Assert
        Assert.Equal(2, result);
    }

    [Fact]
    public void GetValue_WithInvalidType_ThrowsArgumentException()
    {
        // Arrange
        var nonEnumType = typeof(string);
        var enumName = "Value1";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => EnumExtensions.GetValue(nonEnumType, enumName));
    }

    [Fact]
    public void GetValue_WithInvalidName_ThrowsArgumentException()
    {
        // Arrange
        var enumType = typeof(TestEnum);
        var invalidName = "NonExistentValue";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => EnumExtensions.GetValue(enumType, invalidName));
    }

    [Fact]
    public void GetValues_WithGenericType_ReturnsAllValues()
    {
        // Act
        var values = EnumExtensions.GetValues<TestEnum>().ToList();

        // Assert
        Assert.Equal(3, values.Count);
        Assert.Contains(1, values);
        Assert.Contains(2, values);
        Assert.Contains(3, values);
    }

    [Fact]
    public void GetValues_WithType_ReturnsAllValues()
    {
        // Arrange
        var enumType = typeof(TestEnum);

        // Act
        var values = EnumExtensions.GetValues(enumType).ToList();

        // Assert
        Assert.Equal(3, values.Count);
        Assert.Contains(1, values);
        Assert.Contains(2, values);
        Assert.Contains(3, values);
    }

    #endregion

    #region 获取枚举对象测试

    [Fact]
    public void GetEnum_WithValidName_ReturnsCorrectEnum()
    {
        // Arrange
        var enumName = "Value1";

        // Act
        var result = EnumExtensions.GetEnum<TestEnum>(enumName);

        // Assert
        Assert.Equal(TestEnum.Value1, result);
    }

    [Fact]
    public void GetEnum_WithInvalidName_ThrowsArgumentException()
    {
        // Arrange
        var invalidName = "NonExistentValue";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => EnumExtensions.GetEnum<TestEnum>(invalidName));
    }

    [Fact]
    public void GetEnum_WithValidValue_ReturnsCorrectEnum()
    {
        // Arrange
        var enumValue = 2;

        // Act
        var result = EnumExtensions.GetEnum<TestEnum>(enumValue);

        // Assert
        Assert.Equal(TestEnum.Value2, result);
    }

    [Fact]
    public void GetEnum_WithInvalidValue_ThrowsArgumentException()
    {
        // Arrange
        var invalidValue = 999;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => EnumExtensions.GetEnum<TestEnum>(invalidValue));
    }

    [Fact]
    public void GetEnumByDescription_WithValidDescription_ReturnsCorrectEnum()
    {
        // Arrange
        var description = "描述3";

        // Act
        var result = EnumExtensions.GetEnumByDescription<TestEnum>(description);

        // Assert
        Assert.Equal(TestEnum.Value3, result);
    }

    [Fact]
    public void GetEnumByDescription_WithInvalidDescription_ThrowsArgumentException()
    {
        // Arrange
        var invalidDescription = "不存在的描述";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => EnumExtensions.GetEnumByDescription<TestEnum>(invalidDescription));
    }

    [Fact]
    public void GetEnums_ReturnsAllEnumValues()
    {
        // Act
        var enums = EnumExtensions.GetEnums<TestEnum>().ToList();

        // Assert
        Assert.Equal(3, enums.Count);
        Assert.Contains(TestEnum.Value1, enums);
        Assert.Contains(TestEnum.Value2, enums);
        Assert.Contains(TestEnum.Value3, enums);
    }

    #endregion

    #region 获取枚举描述测试

    [Fact]
    public void GetDescription_WithEnumObject_ReturnsCorrectDescription()
    {
        // Arrange
        var enumValue = TestEnum.Value2;

        // Act
        var result = enumValue.GetDescription();

        // Assert
        Assert.Equal("描述2", result);
    }

    [Fact]
    public void GetDescription_WithGenericTypeAndValue_ReturnsCorrectDescription()
    {
        // Arrange
        var enumValue = 3;

        // Act
        var result = EnumExtensions.GetDescription<TestEnum>(enumValue);

        // Assert
        Assert.Equal("描述3", result);
    }

    [Fact]
    public void GetDescription_WithInvalidValue_ReturnsEmptyString()
    {
        // Arrange
        var invalidValue = 999;

        // Act
        var result = EnumExtensions.GetDescription<TestEnum>(invalidValue);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetDescriptions_ReturnsAllDescriptions()
    {
        // Act
        var descriptions = EnumExtensions.GetDescriptions<TestEnum>().ToList();

        // Assert
        Assert.Equal(3, descriptions.Count);
        Assert.Contains("描述1", descriptions);
        Assert.Contains("描述2", descriptions);
        Assert.Contains("描述3", descriptions);
    }

    #endregion

    #region 获取枚举信息测试

    [Fact]
    public void GetEnumInfos_ReturnsCorrectInfos()
    {
        // Arrange
        var enumType = typeof(TestEnum);

        // Act
        var infos = enumType.GetEnumInfos().ToList();

        // Assert
        Assert.Equal(3, infos.Count);

        var info1 = infos.First(i => i.Key == "Value1");
        Assert.Equal(1, info1.Value);
        Assert.Equal("描述1", info1.Label);

        var info2 = infos.First(i => i.Key == "Value2");
        Assert.Equal(2, info2.Value);
        Assert.Equal("描述2", info2.Label);
    }

    [Fact]
    public void GetNameValueDict_ReturnsCorrectDictionary()
    {
        // Act
        var dict = EnumExtensions.GetNameValueDict<TestEnum>();

        // Assert
        Assert.Equal(3, dict.Count);
        Assert.Equal(1, dict["Value1"]);
        Assert.Equal(2, dict["Value2"]);
        Assert.Equal(3, dict["Value3"]);
    }

    [Fact]
    public void GetValueDescriptionDict_ReturnsCorrectDictionary()
    {
        // Act
        var dict = EnumExtensions.GetValueDescriptionDict<TestEnum>();

        // Assert
        Assert.Equal(3, dict.Count);
        Assert.Equal("描述1", dict[1]);
        Assert.Equal("描述2", dict[2]);
        Assert.Equal("描述3", dict[3]);
    }

    #endregion

    #region 枚举检查测试

    [Fact]
    public void IsDefined_WithValidValue_ReturnsTrue()
    {
        // Arrange
        var validValue = 2;

        // Act
        var result = EnumExtensions.IsDefined<TestEnum>(validValue);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsDefined_WithInvalidValue_ReturnsFalse()
    {
        // Arrange
        var invalidValue = 999;

        // Act
        var result = EnumExtensions.IsDefined<TestEnum>(invalidValue);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsDefined_WithValidName_ReturnsTrue()
    {
        // Arrange
        var validName = "Value3";

        // Act
        var result = EnumExtensions.IsDefined<TestEnum>(validName);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsDefined_WithInvalidName_ReturnsFalse()
    {
        // Arrange
        var invalidName = "NonExistentValue";

        // Act
        var result = EnumExtensions.IsDefined<TestEnum>(invalidName);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsDescriptionDefined_WithValidDescription_ReturnsTrue()
    {
        // Arrange
        var validDescription = "描述1";

        // Act
        var result = EnumExtensions.IsDescriptionDefined<TestEnum>(validDescription);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsDescriptionDefined_WithInvalidDescription_ReturnsFalse()
    {
        // Arrange
        var invalidDescription = "不存在的描述";

        // Act
        var result = EnumExtensions.IsDescriptionDefined<TestEnum>(invalidDescription);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region 枚举类型查找测试

    [Fact]
    public void GetEnumTypes_ReturnsAllEnumTypes()
    {
        // Arrange
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var enumTypes = assembly.GetEnumTypes().ToList();

        // Assert
        Assert.Contains(enumTypes, t => t == typeof(TestEnum));
    }

    [Fact]
    public void GetEnumTypeByName_WithValidName_ReturnsCorrectType()
    {
        // Arrange
        var assembly = Assembly.GetExecutingAssembly();
        var typeName = "TestEnum";

        // Act
        var type = assembly.GetEnumTypeByName(typeName);

        // Assert
        Assert.Equal(typeof(TestEnum), type);
    }

    [Fact]
    public void GetEnumTypeByName_WithInvalidName_ReturnsNull()
    {
        // Arrange
        var assembly = Assembly.GetExecutingAssembly();
        var invalidName = "NonExistentEnum";

        // Act
        var type = assembly.GetEnumTypeByName(invalidName);

        // Assert
        Assert.Null(type);
    }

    #endregion
}
