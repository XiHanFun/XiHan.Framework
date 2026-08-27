// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel;
using XiHan.Framework.Utils.Enums;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Utils.Tests.Extensions;

/// <summary>
/// 枚举扩展方法测试
/// </summary>
/// <remarks>
/// HasFlag/CompareTo 这类与 System.Enum 实例方法同名的扩展，用扩展方法语法会被实例方法优先接走，
/// 因此这里一律走静态调用，确保测到的是本仓的实现。
/// </remarks>
public class EnumExtensionsTests
{
    /// <summary>
    /// 有 Description 特性时取描述，没有时回落到字段名
    /// </summary>
    [Fact]
    public void GetDescription_UsesAttributeOrFallsBackToName()
    {
        Assert.Equal("运行中", Status.Running.GetDescription());
        Assert.Equal("空闲", Status.Idle.GetDescription());
        Assert.Equal(nameof(Status.Stopped), Status.Stopped.GetDescription());
    }

    /// <summary>
    /// 有主题特性时取主题，没有时为 null
    /// </summary>
    [Fact]
    public void GetTheme_UsesAttributeOrReturnsNull()
    {
        Assert.Equal("primary", Status.Running.GetTheme());
        Assert.Null(Status.Idle.GetTheme());
    }

    /// <summary>
    /// 显示名称优先取描述
    /// </summary>
    [Fact]
    public void GetDisplayName_PrefersDescription()
    {
        Assert.Equal("运行中", Status.Running.GetDisplayName());
        Assert.Equal(nameof(Status.Stopped), Status.Stopped.GetDisplayName());
    }

    /// <summary>
    /// 已定义的枚举值判为有效，未定义的数值判为无效
    /// </summary>
    [Fact]
    public void IsDefined_DetectsUndefinedValue()
    {
        Assert.True(Status.Running.IsDefined());
        Assert.False(((Status)99).IsDefined());
    }

    /// <summary>
    /// 标志位包含判断与 System.Enum 语义一致
    /// </summary>
    [Fact]
    public void HasFlag_MatchesRuntimeSemantics()
    {
        Assert.True(EnumExtensions.HasFlag(Access.All, Access.Read));
        Assert.True(EnumExtensions.HasFlag(Access.All, Access.Write));
        Assert.False(EnumExtensions.HasFlag(Access.Read, Access.Write));
    }

    /// <summary>
    /// 任意标志与全部标志的判断
    /// </summary>
    [Fact]
    public void HasAnyFlagAndHasAllFlags_CheckFlagSets()
    {
        Assert.True(Access.Read.HasAnyFlag(Access.Read, Access.Write));
        Assert.False(Access.Read.HasAnyFlag(Access.Write));
        Assert.True(Access.All.HasAllFlags(Access.Read, Access.Write));
        Assert.False(Access.Read.HasAllFlags(Access.Read, Access.Write));
    }

    /// <summary>
    /// 空标志数组时任意为假、全部为真
    /// </summary>
    [Fact]
    public void HasAnyFlagAndHasAllFlags_WithEmptyFlags_FollowLinqSemantics()
    {
        Assert.False(Access.Read.HasAnyFlag());
        Assert.True(Access.Read.HasAllFlags());
    }

    /// <summary>
    /// 数值转换保留底层数值
    /// </summary>
    [Fact]
    public void NumericConversions_KeepUnderlyingValue()
    {
        Assert.Equal(2, Status.Stopped.ToInt());
        Assert.Equal(2L, Status.Stopped.ToLong());
        Assert.Equal((byte)2, Status.Stopped.ToByte());
        Assert.Equal((short)2, Status.Stopped.ToShort());
    }

    /// <summary>
    /// 枚举项对象带上键、值、描述与主题
    /// </summary>
    [Fact]
    public void ToEnumItem_FillsKeyValueDescriptionAndTheme()
    {
        var item = Status.Running.ToEnumItem();

        Assert.Equal(nameof(Status.Running), item.Key);
        Assert.Equal(Status.Running, item.Value);
        Assert.Equal("运行中", item.Description);
        Assert.Equal("primary", item.Theme);
    }

    /// <summary>
    /// 添加与移除标志位
    /// </summary>
    [Fact]
    public void AddFlagAndRemoveFlag_UpdateFlagSet()
    {
        Assert.Equal(Access.All, Access.Read.AddFlag(Access.Write));
        Assert.Equal(Access.Write, Access.All.RemoveFlag(Access.Read));
        Assert.Equal(Access.Read, Access.Read.AddFlag(Access.Read));
        Assert.Equal(Access.None, Access.Read.RemoveFlag(Access.Read));
    }

    /// <summary>
    /// 切换标志位在有无之间来回
    /// </summary>
    [Fact]
    public void ToggleFlag_FlipsFlagPresence()
    {
        Assert.Equal(Access.All, Access.Read.ToggleFlag(Access.Write));
        Assert.Equal(Access.Write, Access.All.ToggleFlag(Access.Read));
    }

    /// <summary>
    /// 取出包含的全部标志位，排除零值
    /// </summary>
    [Fact]
    public void GetFlags_ExcludesZeroValue()
    {
        var flags = Access.All.GetFlags().ToList();

        Assert.Contains(Access.Read, flags);
        Assert.Contains(Access.Write, flags);
        Assert.DoesNotContain(Access.None, flags);
    }

    /// <summary>
    /// 非 Flags 枚举调用标志位方法时抛参数异常
    /// </summary>
    [Fact]
    public void FlagOperations_OnNonFlagsEnum_Throw()
    {
        Assert.Throws<ArgumentException>(() => Status.Idle.AddFlag(Status.Running));
        Assert.Throws<ArgumentException>(() => Status.Idle.RemoveFlag(Status.Running));
        Assert.Throws<ArgumentException>(() => Status.Idle.GetFlags().ToList());
    }

    /// <summary>
    /// 取下一个枚举值，末尾时按是否循环决定返回值
    /// </summary>
    [Fact]
    public void GetNext_WalksForwardAndOptionallyLoops()
    {
        Assert.Equal(Status.Running, Status.Idle.GetNext());
        Assert.Null(Status.Stopped.GetNext());
        Assert.Equal(Status.Idle, Status.Stopped.GetNext(true));
        Assert.Null(((Status)99).GetNext());
    }

    /// <summary>
    /// 取上一个枚举值，开头时按是否循环决定返回值
    /// </summary>
    [Fact]
    public void GetPrevious_WalksBackwardAndOptionallyLoops()
    {
        Assert.Equal(Status.Idle, Status.Running.GetPrevious());
        Assert.Null(Status.Idle.GetPrevious());
        Assert.Equal(Status.Stopped, Status.Idle.GetPrevious(true));
        Assert.Null(((Status)99).GetPrevious());
    }

    /// <summary>
    /// 取全部枚举值与名称
    /// </summary>
    [Fact]
    public void GetAllValuesAndNames_ReturnEveryMember()
    {
        Assert.Equal(new[] { Status.Idle, Status.Running, Status.Stopped }, Status.Idle.GetAllValues());
        Assert.Equal(
            new[] { nameof(Status.Idle), nameof(Status.Running), nameof(Status.Stopped) },
            Status.Idle.GetAllNames());
    }

    /// <summary>
    /// 比较按底层数值大小
    /// </summary>
    [Fact]
    public void CompareTo_ComparesUnderlyingValues()
    {
        Assert.True(EnumExtensions.CompareTo(Status.Idle, Status.Stopped) < 0);
        Assert.True(EnumExtensions.CompareTo(Status.Stopped, Status.Idle) > 0);
        Assert.Equal(0, EnumExtensions.CompareTo(Status.Idle, Status.Idle));
    }

    /// <summary>
    /// 区间判断包含两端
    /// </summary>
    [Fact]
    public void IsBetween_IncludesEndpoints()
    {
        Assert.True(Status.Running.IsBetween(Status.Idle, Status.Stopped));
        Assert.True(Status.Idle.IsBetween(Status.Idle, Status.Stopped));
        Assert.True(Status.Stopped.IsBetween(Status.Idle, Status.Stopped));
        Assert.False(Status.Stopped.IsBetween(Status.Idle, Status.Running));
    }

    /// <summary>
    /// 指定格式串输出枚举文本
    /// </summary>
    [Fact]
    public void ToStringWithFormat_SupportsNameAndNumericForms()
    {
        Assert.Equal("Running", EnumExtensions.ToString<Status>(Status.Running, "G"));
        Assert.Equal("1", EnumExtensions.ToString<Status>(Status.Running, "D"));
    }

    /// <summary>
    /// JSON 文本默认用名称，指定后用描述
    /// </summary>
    [Fact]
    public void ToJsonString_SwitchesBetweenNameAndDescription()
    {
        Assert.Equal("\"Running\"", Status.Running.ToJsonString());
        Assert.Equal("\"运行中\"", Status.Running.ToJsonString(true));
    }

    /// <summary>
    /// 测试用普通枚举，混合有无描述与主题的成员
    /// </summary>
    private enum Status
    {
        /// <summary>
        /// 空闲
        /// </summary>
        [Description("空闲")]
        Idle = 0,

        /// <summary>
        /// 运行中
        /// </summary>
        [Description("运行中")]
        [EnumTheme("primary")]
        Running = 1,

        /// <summary>
        /// 无任何特性的成员
        /// </summary>
        Stopped = 2
    }

    /// <summary>
    /// 测试用标志位枚举
    /// </summary>
    [Flags]
    private enum Access
    {
        /// <summary>
        /// 无权限
        /// </summary>
        None = 0,

        /// <summary>
        /// 读
        /// </summary>
        Read = 1,

        /// <summary>
        /// 写
        /// </summary>
        Write = 2,

        /// <summary>
        /// 读写
        /// </summary>
        All = Read | Write
    }
}
