// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using XiHan.Framework.Utils.Reflections;
// 与 BCL 的 System.Reflection.MemberInfoExtensions 同名，用别名锚定到被测的那个。
using FrameworkMemberInfoExtensions = XiHan.Framework.Utils.Reflections.MemberInfoExtensions;

namespace XiHan.Framework.Utils.Tests.Reflections;

/// <summary>
/// 成员信息扩展方法测试
/// </summary>
/// <remarks>
/// GetDescription 的取值有明确优先级：Description &gt; DisplayName &gt; Display.Name &gt; 成员名，
/// 这套顺序被大量前端元数据依赖，用例逐级钉死。
/// </remarks>
public class MemberInfoExtensionsTests
{
    /// <summary>
    /// Description 优先级最高
    /// </summary>
    [Fact]
    public void GetDescription_PrefersDescriptionAttribute()
    {
        var member = GetProperty(nameof(Sample.WithDescription));

        Assert.Equal("描述", member.GetDescription());
    }

    /// <summary>
    /// 没有 Description 时取 DisplayName
    /// </summary>
    [Fact]
    public void GetDescription_FallsBackToDisplayName()
    {
        var member = GetProperty(nameof(Sample.WithDisplayName));

        Assert.Equal("显示名", member.GetDescription());
    }

    /// <summary>
    /// 前两者都没有时取 Display 的名称
    /// </summary>
    [Fact]
    public void GetDescription_FallsBackToDisplayAttributeName()
    {
        var member = GetProperty(nameof(Sample.WithDisplay));

        Assert.Equal("展示名", member.GetDescription());
    }

    /// <summary>
    /// 什么特性都没有时回落到成员名
    /// </summary>
    [Fact]
    public void GetDescription_FallsBackToMemberName()
    {
        var member = GetProperty(nameof(Sample.Plain));

        Assert.Equal(nameof(Sample.Plain), member.GetDescription());
    }

    /// <summary>
    /// 特性存在性判断
    /// </summary>
    [Fact]
    public void HasAttribute_DetectsPresence()
    {
        Assert.True(GetProperty(nameof(Sample.WithDescription)).HasAttribute<DescriptionAttribute>());
        Assert.False(GetProperty(nameof(Sample.Plain)).HasAttribute<DescriptionAttribute>());
    }

    /// <summary>
    /// 取单个特性，不存在时返回 null
    /// </summary>
    [Fact]
    public void GetSingleAttributeOrNull_ReturnsAttributeOrNull()
    {
        var attribute = GetProperty(nameof(Sample.WithDescription)).GetSingleAttributeOrNull<DescriptionAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("描述", attribute!.Description);
        Assert.Null(GetProperty(nameof(Sample.Plain)).GetSingleAttributeOrNull<DescriptionAttribute>());
    }

    /// <summary>
    /// 成员为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void GetSingleAttributeOrNull_WhenMemberIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => FrameworkMemberInfoExtensions.GetSingleAttributeOrNull<DescriptionAttribute>(null!));
    }

    /// <summary>
    /// 沿基类链向上查找类型特性
    /// </summary>
    [Fact]
    public void GetSingleAttributeOfTypeOrBaseTypesOrNull_WalksUpBaseTypes()
    {
        var attribute = typeof(DerivedType).GetSingleAttributeOfTypeOrBaseTypesOrNull<DescriptionAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("基类描述", attribute!.Description);
    }

    /// <summary>
    /// 整条基类链都没有该特性时返回 null
    /// </summary>
    [Fact]
    public void GetSingleAttributeOfTypeOrBaseTypesOrNull_WhenNothingFound_ReturnsNull()
    {
        Assert.Null(typeof(PlainType).GetSingleAttributeOfTypeOrBaseTypesOrNull<DescriptionAttribute>());
    }

    /// <summary>
    /// 取测试类型的属性成员信息
    /// </summary>
    private static MemberInfo GetProperty(string name)
    {
        return typeof(Sample).GetProperty(name)!;
    }

    /// <summary>
    /// 测试用承载类型：每个属性携带一种元数据特性
    /// </summary>
    private sealed class Sample
    {
        /// <summary>
        /// 带 Description
        /// </summary>
        [Description("描述")]
        public string WithDescription { get; set; } = string.Empty;

        /// <summary>
        /// 带 DisplayName
        /// </summary>
        [DisplayName("显示名")]
        public string WithDisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 带 Display
        /// </summary>
        [Display(Name = "展示名")]
        public string WithDisplay { get; set; } = string.Empty;

        /// <summary>
        /// 不带任何特性
        /// </summary>
        public string Plain { get; set; } = string.Empty;
    }

    /// <summary>
    /// 带类型级描述的基类
    /// </summary>
    [Description("基类描述")]
    private class BaseType
    {
    }

    /// <summary>
    /// 继承自带描述基类的派生类
    /// </summary>
    private sealed class DerivedType : BaseType
    {
    }

    /// <summary>
    /// 整条链都不带描述的类型
    /// </summary>
    private sealed class PlainType
    {
    }
}
