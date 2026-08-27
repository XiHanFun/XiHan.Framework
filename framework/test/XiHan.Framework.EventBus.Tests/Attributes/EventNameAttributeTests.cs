// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.EventBus.Attributes;
using XiHan.Framework.EventBus.Tests.Fakes;

namespace XiHan.Framework.EventBus.Tests.Attributes;

/// <summary>
/// 事件名称特性测试
/// </summary>
/// <remarks>
/// 事件名是跨进程契约的一部分（发件箱记录、Broker 路由键都按它落库/路由），
/// 因此这里锁死解析规则本身，而不是某个具体字符串。
/// </remarks>
public class EventNameAttributeTests
{
    /// <summary>
    /// 未标注特性的事件，事件名回落到类型全名
    /// </summary>
    [Fact]
    public void GetNameOrDefault_WhenTypeHasNoAttribute_FallsBackToFullName()
    {
        Assert.Equal(typeof(PlainNoticeEvent).FullName, EventNameAttribute.GetNameOrDefault(typeof(PlainNoticeEvent)));
    }

    /// <summary>
    /// 标注了特性的事件，事件名取特性声明值
    /// </summary>
    [Fact]
    public void GetNameOrDefault_WhenTypeHasAttribute_ReturnsDeclaredName()
    {
        Assert.Equal(NamedNoticeEvent.DeclaredEventName, EventNameAttribute.GetNameOrDefault(typeof(NamedNoticeEvent)));
    }

    /// <summary>
    /// 泛型重载与非泛型重载解析结果一致
    /// </summary>
    [Fact]
    public void GetNameOrDefaultOfTEvent_MatchesTypeOverload()
    {
        Assert.Equal(
            EventNameAttribute.GetNameOrDefault(typeof(NamedNoticeEvent)),
            EventNameAttribute.GetNameOrDefault<NamedNoticeEvent>());
    }

    /// <summary>
    /// 特性可被派生事件继承，派生事件沿用基类事件名
    /// </summary>
    /// <remarks>
    /// 这是 .NET 特性继承的既有语义，会让派生事件与基类共用同一个跨进程事件名，
    /// 需要区分时必须在派生类上重新标注。
    /// </remarks>
    [Fact]
    public void GetNameOrDefault_WhenDerivedFromAnnotatedType_InheritsBaseName()
    {
        Assert.Equal(NamedNoticeEvent.DeclaredEventName, EventNameAttribute.GetNameOrDefault(typeof(InheritedNameEvent)));
    }

    /// <summary>
    /// 派生事件重新标注后使用自己的事件名
    /// </summary>
    [Fact]
    public void GetNameOrDefault_WhenDerivedTypeReannotated_UsesOwnName()
    {
        Assert.Equal("xihan.tests.reannotated", EventNameAttribute.GetNameOrDefault(typeof(ReannotatedNameEvent)));
    }

    /// <summary>
    /// 事件类型为空时抛出参数异常
    /// </summary>
    [Fact]
    public void GetNameOrDefault_WhenEventTypeNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            EventNameAttribute.GetNameOrDefault(null!);
        });
    }

    /// <summary>
    /// 特性名称为空或空白时构造失败
    /// </summary>
    /// <param name="name">事件名称</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_WhenNameNullOrWhiteSpace_Throws(string? name)
    {
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new EventNameAttribute(name!);
        });
    }

    /// <summary>
    /// 特性保留构造时传入的名称
    /// </summary>
    [Fact]
    public void Name_ReturnsCtorArgument()
    {
        var attribute = new EventNameAttribute("xihan.tests.explicit");

        Assert.Equal("xihan.tests.explicit", attribute.Name);
    }

    /// <summary>
    /// 特性的名称解析与传入的事件类型无关
    /// </summary>
    [Fact]
    public void GetName_IgnoresEventTypeArgument()
    {
        var attribute = new EventNameAttribute("xihan.tests.explicit");

        Assert.Equal("xihan.tests.explicit", attribute.GetName(typeof(PlainNoticeEvent)));
    }
}

/// <summary>
/// 测试事件：派生自带事件名的事件但未重新标注
/// </summary>
public class InheritedNameEvent : NamedNoticeEvent
{
}

/// <summary>
/// 测试事件：派生自带事件名的事件并重新标注
/// </summary>
[EventName("xihan.tests.reannotated")]
public class ReannotatedNameEvent : NamedNoticeEvent
{
}
