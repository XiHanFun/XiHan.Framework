// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.EventBus.Abstractions.Distributed;

namespace XiHan.Framework.EventBus.Abstractions.Tests;

/// <summary>
/// 发件箱配置字典测试
/// </summary>
/// <remarks>
/// 与收件箱配置字典同构，默认键同样是 "Default"。
/// 两个字典的默认键必须一致，否则同一份模块配置在收发两侧会落到不同的箱子上。
/// </remarks>
public class OutboxConfigDictionaryTests
{
    /// <summary>
    /// 无名重载写入约定的默认发件箱
    /// </summary>
    [Fact]
    public void Configure_WithoutName_UsesDefaultKey()
    {
        var dictionary = new OutboxConfigDictionary();

        dictionary.Configure(config => config.DatabaseName = "EventStore");

        Assert.True(dictionary.ContainsKey("Default"));
        Assert.Equal("Default", dictionary["Default"].Name);
        Assert.Equal("EventStore", dictionary["Default"].DatabaseName);
    }

    /// <summary>
    /// 指定名称时以该名称建项
    /// </summary>
    [Fact]
    public void Configure_WithName_CreatesEntryWithMatchingName()
    {
        var dictionary = new OutboxConfigDictionary();

        dictionary.Configure("Audit", config => config.DatabaseName = "AuditStore");

        Assert.True(dictionary.ContainsKey("Audit"));
        Assert.Equal("Audit", dictionary["Audit"].Name);
    }

    /// <summary>
    /// 同名重复配置作用在同一实例上，前一次配置不丢失
    /// </summary>
    [Fact]
    public void Configure_CalledTwiceWithSameName_AccumulatesOnSameInstance()
    {
        var dictionary = new OutboxConfigDictionary();

        dictionary.Configure("Audit", config => config.DatabaseName = "AuditStore");
        var first = dictionary["Audit"];

        dictionary.Configure("Audit", config => config.IsSendingEnabled = false);
        var second = dictionary["Audit"];

        Assert.Same(first, second);
        Assert.Single(dictionary);
        Assert.Equal("AuditStore", second.DatabaseName);
        Assert.False(second.IsSendingEnabled);
    }

    /// <summary>
    /// 收发两侧的默认键一致
    /// </summary>
    [Fact]
    public void Configure_WithoutName_UsesSameDefaultKeyAsInbox()
    {
        var outboxes = new OutboxConfigDictionary();
        var inboxes = new InboxConfigDictionary();

        outboxes.Configure(config => config.DatabaseName = "EventStore");
        inboxes.Configure(config => config.DatabaseName = "EventStore");

        Assert.True(outboxes.ContainsKey("Default"));
        Assert.True(inboxes.ContainsKey("Default"));
    }

    /// <summary>
    /// 名称为 null 时按空引用拒绝
    /// </summary>
    [Fact]
    public void Configure_WhenNameIsNull_ThrowsArgumentNullException()
    {
        var dictionary = new OutboxConfigDictionary();

        Assert.Throws<ArgumentNullException>(() =>
        {
            dictionary.Configure(null!, config => config.IsSendingEnabled = false);
        });
    }

    /// <summary>
    /// 名称为空白时按非法参数拒绝
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Configure_WhenNameIsBlank_ThrowsArgumentException(string name)
    {
        var dictionary = new OutboxConfigDictionary();

        Assert.Throws<ArgumentException>(() =>
        {
            dictionary.Configure(name, config => config.IsSendingEnabled = false);
        });
    }

    /// <summary>
    /// 配置委托为空时拒绝，且不留下半成品条目
    /// </summary>
    [Fact]
    public void Configure_WhenActionIsNull_ThrowsAndLeavesDictionaryUntouched()
    {
        var dictionary = new OutboxConfigDictionary();

        Assert.Throws<ArgumentNullException>(() =>
        {
            dictionary.Configure("Audit", null!);
        });

        Assert.Empty(dictionary);
    }

    /// <summary>
    /// 字典本身仍是普通字典，可直接按键读写
    /// </summary>
    [Fact]
    public void Dictionary_IsPlainStringKeyedDictionary()
    {
        var dictionary = new OutboxConfigDictionary();

        Assert.IsAssignableFrom<Dictionary<string, OutboxConfig>>(dictionary);
        Assert.Empty(dictionary);
    }
}
