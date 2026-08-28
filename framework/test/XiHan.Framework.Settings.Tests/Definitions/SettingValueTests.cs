// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core;
using XiHan.Framework.Settings.Definitions;

namespace XiHan.Framework.Settings.Tests.Definitions;

/// <summary>
/// 设置值测试
/// </summary>
/// <remarks>
/// 设置值是存储层与提供者层之间的传输契约，值必须允许为 null（表示"该层未命中"），
/// 因此泛型参数固定为可空字符串，这一点不能退化成不可空。
/// </remarks>
public class SettingValueTests
{
    /// <summary>
    /// 双参构造器同时落名称与值
    /// </summary>
    [Fact]
    public void Ctor_WithNameAndValue_AssignsBoth()
    {
        var settingValue = new SettingValue("Foo", "bar");

        Assert.Equal("Foo", settingValue.Name);
        Assert.Equal("bar", settingValue.Value);
    }

    /// <summary>
    /// 值允许为 null，表示该层没有命中
    /// </summary>
    [Fact]
    public void Ctor_AllowsNullValue()
    {
        var settingValue = new SettingValue("Foo", null);

        Assert.Equal("Foo", settingValue.Name);
        Assert.Null(settingValue.Value);
    }

    /// <summary>
    /// 无参构造器不预置任何值
    /// </summary>
    [Fact]
    public void Ctor_Parameterless_LeavesValueNull()
    {
        var settingValue = new SettingValue();

        Assert.Null(settingValue.Value);
    }

    /// <summary>
    /// 名称与值在构造后仍可改写
    /// </summary>
    [Fact]
    public void Properties_AreMutableAfterConstruction()
    {
        var settingValue = new SettingValue("Foo", "bar")
        {
            Name = "Renamed",
            Value = null
        };

        Assert.Equal("Renamed", settingValue.Name);
        Assert.Null(settingValue.Value);
    }

    /// <summary>
    /// 设置值继承自可空字符串的名值对
    /// </summary>
    [Fact]
    public void SettingValue_IsNameValueOfNullableString()
    {
        var settingValue = new SettingValue("Foo", "bar");

        Assert.IsAssignableFrom<NameValue<string?>>(settingValue);
    }

    /// <summary>
    /// 设置值是普通类而非记录，同名同值的两个实例不相等
    /// </summary>
    [Fact]
    public void SettingValue_UsesReferenceEquality()
    {
        var first = new SettingValue("Foo", "bar");
        var second = new SettingValue("Foo", "bar");

        Assert.NotEqual(first, second);
        Assert.Equal(first, first);
    }
}
