// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.EventBus.Abstractions.Distributed;

namespace XiHan.Framework.EventBus.Abstractions.Tests;

/// <summary>
/// 收件箱配置字典测试
/// </summary>
/// <remarks>
/// 配置字典的核心语义是「按名取或建、重复调用累积而不是覆盖」：
/// 模块化装配下多个模块会各自调一次 Configure 往同一个收件箱上叠配置，
/// 若第二次调用新建了实例，前一个模块的配置就会被静默丢弃。
/// </remarks>
public class InboxConfigDictionaryTests
{
    /// <summary>
    /// 无名重载写入约定的默认收件箱
    /// </summary>
    [Fact]
    public void Configure_WithoutName_UsesDefaultKey()
    {
        var dictionary = new InboxConfigDictionary();

        dictionary.Configure(config => config.DatabaseName = "EventStore");

        Assert.True(dictionary.ContainsKey("Default"));
        Assert.Equal("Default", dictionary["Default"].Name);
        Assert.Equal("EventStore", dictionary["Default"].DatabaseName);
    }

    /// <summary>
    /// 指定名称时以该名称建项，且配置对象的名称与键一致
    /// </summary>
    [Fact]
    public void Configure_WithName_CreatesEntryWithMatchingName()
    {
        var dictionary = new InboxConfigDictionary();

        dictionary.Configure("Audit", config => config.DatabaseName = "AuditStore");

        Assert.True(dictionary.ContainsKey("Audit"));

        var config = dictionary["Audit"];
        Assert.Equal("Audit", config.Name);
        Assert.Equal("AuditStore", config.DatabaseName);
    }

    /// <summary>
    /// 同名重复配置作用在同一实例上，前一次配置不丢失
    /// </summary>
    [Fact]
    public void Configure_CalledTwiceWithSameName_AccumulatesOnSameInstance()
    {
        var dictionary = new InboxConfigDictionary();

        dictionary.Configure("Audit", config => config.DatabaseName = "AuditStore");
        var first = dictionary["Audit"];

        dictionary.Configure("Audit", config => config.IsProcessingEnabled = false);
        var second = dictionary["Audit"];

        Assert.Same(first, second);
        Assert.Single(dictionary);
        Assert.Equal("AuditStore", second.DatabaseName);
        Assert.False(second.IsProcessingEnabled);
    }

    /// <summary>
    /// 不同名称各自独立成项
    /// </summary>
    [Fact]
    public void Configure_WithDifferentNames_CreatesSeparateEntries()
    {
        var dictionary = new InboxConfigDictionary();

        dictionary.Configure(config => config.DatabaseName = "DefaultStore");
        dictionary.Configure("Audit", config => config.DatabaseName = "AuditStore");

        Assert.Equal(2, dictionary.Count);
        Assert.NotSame(dictionary["Default"], dictionary["Audit"]);
    }

    /// <summary>
    /// 名称为 null 时按空引用拒绝
    /// </summary>
    [Fact]
    public void Configure_WhenNameIsNull_ThrowsArgumentNullException()
    {
        var dictionary = new InboxConfigDictionary();

        Assert.Throws<ArgumentNullException>(() =>
        {
            dictionary.Configure(null!, config => config.IsProcessingEnabled = false);
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
        var dictionary = new InboxConfigDictionary();

        Assert.Throws<ArgumentException>(() =>
        {
            dictionary.Configure(name, config => config.IsProcessingEnabled = false);
        });
    }

    /// <summary>
    /// 配置委托为空时拒绝，且不留下半成品条目
    /// </summary>
    [Fact]
    public void Configure_WhenActionIsNull_ThrowsAndLeavesDictionaryUntouched()
    {
        var dictionary = new InboxConfigDictionary();

        Assert.Throws<ArgumentNullException>(() =>
        {
            dictionary.Configure("Audit", null!);
        });

        Assert.Empty(dictionary);
    }

    /// <summary>
    /// 无名重载在委托为空时同样拒绝
    /// </summary>
    [Fact]
    public void Configure_WithoutName_WhenActionIsNull_Throws()
    {
        var dictionary = new InboxConfigDictionary();

        Assert.Throws<ArgumentNullException>(() =>
        {
            dictionary.Configure((Action<InboxConfig>)null!);
        });
    }

    /// <summary>
    /// 字典本身仍是普通字典，可直接按键读写
    /// </summary>
    [Fact]
    public void Dictionary_IsPlainStringKeyedDictionary()
    {
        var dictionary = new InboxConfigDictionary();

        Assert.IsAssignableFrom<Dictionary<string, InboxConfig>>(dictionary);
        Assert.Empty(dictionary);
    }
}
