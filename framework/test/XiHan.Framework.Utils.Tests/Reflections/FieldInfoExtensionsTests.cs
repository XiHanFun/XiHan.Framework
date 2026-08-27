// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using XiHan.Framework.Utils.Reflections;

namespace XiHan.Framework.Utils.Tests.Reflections;

/// <summary>
/// 字段信息扩展方法测试
/// </summary>
public class FieldInfoExtensionsTests
{
    /// <summary>
    /// 有 Description 特性时取描述值
    /// </summary>
    [Fact]
    public void GetDescriptionValue_WithAttribute_ReturnsDescription()
    {
        var field = GetField(nameof(Marker.Described));

        Assert.Equal("有描述", field.GetDescriptionValue());
    }

    /// <summary>
    /// 没有 Description 特性时回落到字段名
    /// </summary>
    [Fact]
    public void GetDescriptionValue_WithoutAttribute_ReturnsFieldName()
    {
        var field = GetField(nameof(Marker.Plain));

        Assert.Equal(nameof(Marker.Plain), field.GetDescriptionValue());
    }

    /// <summary>
    /// Display 特性带描述时取其描述
    /// </summary>
    [Fact]
    public void GetDisplayValue_WithDisplayDescription_ReturnsIt()
    {
        var field = GetField(nameof(Marker.Displayed));

        Assert.Equal("显示描述", field.GetDisplayValue());
    }

    /// <summary>
    /// Display 特性只给了名称时仍回落到字段名
    /// </summary>
    [Fact]
    public void GetDisplayValue_WhenDisplayHasNoDescription_ReturnsFieldName()
    {
        var field = GetField(nameof(Marker.NameOnly));

        Assert.Equal(nameof(Marker.NameOnly), field.GetDisplayValue());
    }

    /// <summary>
    /// 没有 Display 特性时回落到字段名
    /// </summary>
    [Fact]
    public void GetDisplayValue_WithoutAttribute_ReturnsFieldName()
    {
        var field = GetField(nameof(Marker.Plain));

        Assert.Equal(nameof(Marker.Plain), field.GetDisplayValue());
    }

    /// <summary>
    /// 两个方法互不干扰：只标了 Description 的字段不会被 Display 取到
    /// </summary>
    [Fact]
    public void GetDescriptionValueAndGetDisplayValue_ReadDifferentAttributes()
    {
        var field = GetField(nameof(Marker.Described));

        Assert.Equal("有描述", field.GetDescriptionValue());
        Assert.Equal(nameof(Marker.Described), field.GetDisplayValue());
    }

    /// <summary>
    /// 取测试枚举的字段信息
    /// </summary>
    private static FieldInfo GetField(string name)
    {
        return typeof(Marker).GetField(name)!;
    }

    /// <summary>
    /// 测试用枚举：不同成员携带不同的元数据特性
    /// </summary>
    private enum Marker
    {
        /// <summary>
        /// 只带 Description
        /// </summary>
        [Description("有描述")]
        Described = 0,

        /// <summary>
        /// 不带任何特性
        /// </summary>
        Plain = 1,

        /// <summary>
        /// Display 带描述
        /// </summary>
        [Display(Description = "显示描述")]
        Displayed = 2,

        /// <summary>
        /// Display 只带名称
        /// </summary>
        [Display(Name = "只有名称")]
        NameOnly = 3
    }
}
