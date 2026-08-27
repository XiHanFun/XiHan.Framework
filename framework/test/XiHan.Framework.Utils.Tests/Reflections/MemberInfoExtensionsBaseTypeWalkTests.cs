// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel;
using XiHan.Framework.Utils.Reflections;

namespace XiHan.Framework.Utils.Tests.Reflections;

/// <summary>
/// 沿基类链取特性时的空入参回归测试
/// </summary>
/// <remarks>
/// GetSingleAttributeOfTypeOrBaseTypesOrNull 的签名是 Type?，null 是合法入参，
/// 但原实现的 while (true) 只在 `type is not null &amp;&amp; BaseType is null` 时退出：
/// 传 null 时这个条件恒为假，末尾又把 null 原样赋回 type，循环永远不退出、挂死线程。
/// 用例加超时，缺陷未修时会以超时失败而不是无限挂住。
/// </remarks>
public class MemberInfoExtensionsBaseTypeWalkTests
{
    /// <summary>
    /// 入参为 null 时立即返回 null，不会死循环
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void GetSingleAttributeOfTypeOrBaseTypesOrNull_WhenTypeIsNull_ReturnsNull()
    {
        Assert.Null(MemberInfoExtensions.GetSingleAttributeOfTypeOrBaseTypesOrNull<DescriptionAttribute>(null));
    }

    /// <summary>
    /// 接口类型没有基类，同样立即返回 null
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void GetSingleAttributeOfTypeOrBaseTypesOrNull_WhenInterfaceHasNoAttribute_ReturnsNull()
    {
        Assert.Null(typeof(IMarker).GetSingleAttributeOfTypeOrBaseTypesOrNull<DescriptionAttribute>());
    }

    /// <summary>
    /// 自身带特性时直接返回自身的特性
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void GetSingleAttributeOfTypeOrBaseTypesOrNull_WhenSelfHasAttribute_ReturnsOwn()
    {
        var attribute = typeof(TaggedBase).GetSingleAttributeOfTypeOrBaseTypesOrNull<DescriptionAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("基类说明", attribute!.Description);
    }

    /// <summary>
    /// 自身没有时沿基类链继续向上找
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void GetSingleAttributeOfTypeOrBaseTypesOrNull_WalksUpToBaseType()
    {
        var attribute = typeof(UntaggedDerived).GetSingleAttributeOfTypeOrBaseTypesOrNull<DescriptionAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("基类说明", attribute!.Description);
    }

    /// <summary>
    /// 整条基类链上都没有时返回 null
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void GetSingleAttributeOfTypeOrBaseTypesOrNull_WhenNothingOnChain_ReturnsNull()
    {
        Assert.Null(typeof(PlainDerived).GetSingleAttributeOfTypeOrBaseTypesOrNull<DescriptionAttribute>());
    }

    /// <summary>
    /// 测试用标记接口
    /// </summary>
    private interface IMarker
    {
    }

    /// <summary>
    /// 测试用带特性基类
    /// </summary>
    [Description("基类说明")]
    private class TaggedBase
    {
    }

    /// <summary>
    /// 测试用不带特性的派生类
    /// </summary>
    private sealed class UntaggedDerived : TaggedBase
    {
    }

    /// <summary>
    /// 测试用整条链都不带特性的类型
    /// </summary>
    private sealed class PlainDerived
    {
    }
}
