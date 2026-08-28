// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.EventBus.Attributes;
using XiHan.Framework.EventBus.Tests.Fakes;

namespace XiHan.Framework.EventBus.Tests.Attributes;

/// <summary>
/// 通用事件名称特性测试
/// </summary>
/// <remarks>
/// 泛型事件的事件名由「泛型参数的事件名」加前后缀拼装，
/// 这里覆盖拼装规则与两条非法输入分支（非泛型、多泛型参数）。
/// </remarks>
public class GenericEventNameAttributeTests
{
    /// <summary>
    /// 前后缀同时配置时按前缀 + 内层事件名 + 后缀拼装
    /// </summary>
    [Fact]
    public void GetName_WithPrefixAndPostfix_WrapsInnerEventName()
    {
        var expected = "xihan." + NamedNoticeEvent.DeclaredEventName + ".created";

        Assert.Equal(expected, EventNameAttribute.GetNameOrDefault(typeof(GenericNoticeEvent<NamedNoticeEvent>)));
    }

    /// <summary>
    /// 内层类型没有事件名特性时，用内层类型全名参与拼装
    /// </summary>
    [Fact]
    public void GetName_WhenInnerTypeHasNoAttribute_UsesInnerFullName()
    {
        var expected = "xihan." + typeof(PlainNoticeEvent).FullName + ".created";

        Assert.Equal(expected, EventNameAttribute.GetNameOrDefault(typeof(GenericNoticeEvent<PlainNoticeEvent>)));
    }

    /// <summary>
    /// 未配置前后缀时事件名与内层事件名完全一致
    /// </summary>
    [Fact]
    public void GetName_WithoutAffixes_ReturnsInnerEventName()
    {
        Assert.Equal(
            NamedNoticeEvent.DeclaredEventName,
            EventNameAttribute.GetNameOrDefault(typeof(BareGenericNoticeEvent<NamedNoticeEvent>)));
    }

    /// <summary>
    /// 只配置前缀时不追加后缀
    /// </summary>
    [Fact]
    public void GetName_WithPrefixOnly_AppendsNothingAtTail()
    {
        Assert.Equal(
            "only-prefix." + NamedNoticeEvent.DeclaredEventName,
            EventNameAttribute.GetNameOrDefault(typeof(PrefixOnlyEvent<NamedNoticeEvent>)));
    }

    /// <summary>
    /// 只配置后缀时不追加前缀
    /// </summary>
    [Fact]
    public void GetName_WithPostfixOnly_PrependsNothingAtHead()
    {
        Assert.Equal(
            NamedNoticeEvent.DeclaredEventName + ".only-postfix",
            EventNameAttribute.GetNameOrDefault(typeof(PostfixOnlyEvent<NamedNoticeEvent>)));
    }

    /// <summary>
    /// 前后缀为空串时视同未配置，不引入多余分隔
    /// </summary>
    [Fact]
    public void GetName_WithEmptyAffixes_ReturnsInnerEventName()
    {
        Assert.Equal(
            NamedNoticeEvent.DeclaredEventName,
            EventNameAttribute.GetNameOrDefault(typeof(EmptyAffixEvent<NamedNoticeEvent>)));
    }

    /// <summary>
    /// 前后缀属性默认未配置
    /// </summary>
    [Fact]
    public void Affixes_AreNullByDefault()
    {
        var attribute = new GenericEventNameAttribute();

        Assert.Null(attribute.Prefix);
        Assert.Null(attribute.Postfix);
    }

    /// <summary>
    /// 作用于非泛型类型时抛出框架异常
    /// </summary>
    [Fact]
    public void GetName_WhenTypeIsNotGeneric_Throws()
    {
        var attribute = new GenericEventNameAttribute();

        var exception = Assert.Throws<XiHanException>(() =>
        {
            attribute.GetName(typeof(NamedNoticeEvent));
        });
        Assert.Contains("不是泛型类型", exception.Message);
    }

    /// <summary>
    /// 作用于多泛型参数类型时抛出框架异常
    /// </summary>
    [Fact]
    public void GetName_WhenTypeHasMultipleGenericArguments_Throws()
    {
        var attribute = new GenericEventNameAttribute();

        var exception = Assert.Throws<XiHanException>(() =>
        {
            attribute.GetName(typeof(Dictionary<string, int>));
        });
        Assert.Contains("多个泛型参数", exception.Message);
    }

    /// <summary>
    /// 同一泛型定义在不同泛型参数下产出不同事件名
    /// </summary>
    [Fact]
    public void GetName_ForDifferentGenericArguments_ProducesDifferentNames()
    {
        var first = EventNameAttribute.GetNameOrDefault(typeof(GenericNoticeEvent<NamedNoticeEvent>));
        var second = EventNameAttribute.GetNameOrDefault(typeof(GenericNoticeEvent<PlainNoticeEvent>));

        Assert.NotEqual(first, second);
    }
}

/// <summary>
/// 测试事件：只配置前缀的泛型事件
/// </summary>
/// <typeparam name="TPayload">载荷类型</typeparam>
[GenericEventName(Prefix = "only-prefix.")]
public class PrefixOnlyEvent<TPayload>
{
}

/// <summary>
/// 测试事件：只配置后缀的泛型事件
/// </summary>
/// <typeparam name="TPayload">载荷类型</typeparam>
[GenericEventName(Postfix = ".only-postfix")]
public class PostfixOnlyEvent<TPayload>
{
}

/// <summary>
/// 测试事件：前后缀配置为空串的泛型事件
/// </summary>
/// <typeparam name="TPayload">载荷类型</typeparam>
[GenericEventName(Prefix = "", Postfix = "")]
public class EmptyAffixEvent<TPayload>
{
}
