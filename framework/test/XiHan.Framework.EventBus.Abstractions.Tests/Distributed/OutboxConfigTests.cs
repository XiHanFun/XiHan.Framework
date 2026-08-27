// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.EventBus.Abstractions.Distributed;

namespace XiHan.Framework.EventBus.Abstractions.Tests;

/// <summary>
/// 发件箱配置测试
/// </summary>
/// <remarks>
/// 与收件箱配置形状相近但不完全对称：发件箱只有一个 <c>Selector</c>（发件侧没有处理器概念），
/// 开关叫 <c>IsSendingEnabled</c>。这两处命名差异容易被复制粘贴写错，因此单独锁定。
/// </remarks>
public class OutboxConfigTests
{
    /// <summary>
    /// 构造函数写入的名称原样暴露
    /// </summary>
    [Fact]
    public void Ctor_WithName_ExposesSameName()
    {
        var config = new OutboxConfig("Default");

        Assert.Equal("Default", config.Name);
    }

    /// <summary>
    /// 名称为空时构造失败
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_WhenNameIsBlank_ThrowsArgumentException(string? name)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            _ = new OutboxConfig(name!);
        });

        Assert.Equal("name", exception.ParamName);
    }

    /// <summary>
    /// 名称只读，构造后不可改写
    /// </summary>
    [Fact]
    public void Name_IsReadOnly()
    {
        var property = typeof(OutboxConfig).GetProperty(nameof(OutboxConfig.Name));

        Assert.NotNull(property);
        Assert.False(property.CanWrite);
    }

    /// <summary>
    /// 数据库名称写入后可读回
    /// </summary>
    [Fact]
    public void DatabaseName_WhenAssigned_IsReadBack()
    {
        var config = new OutboxConfig("Default")
        {
            DatabaseName = "EventStore"
        };

        Assert.Equal("EventStore", config.DatabaseName);
    }

    /// <summary>
    /// 数据库名称为空时赋值失败
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DatabaseName_WhenAssignedBlank_ThrowsArgumentException(string? databaseName)
    {
        var config = new OutboxConfig("Default");

        var exception = Assert.Throws<ArgumentException>(() =>
        {
            config.DatabaseName = databaseName!;
        });

        Assert.Equal(nameof(OutboxConfig.DatabaseName), exception.ParamName);
    }

    /// <summary>
    /// 默认启用发件箱发送
    /// </summary>
    [Fact]
    public void IsSendingEnabled_ByDefault_IsTrue()
    {
        var config = new OutboxConfig("Default");

        Assert.True(config.IsSendingEnabled);
    }

    /// <summary>
    /// 可关闭发件箱发送（只入库不外发）
    /// </summary>
    [Fact]
    public void IsSendingEnabled_CanBeDisabled()
    {
        var config = new OutboxConfig("Default")
        {
            IsSendingEnabled = false
        };

        Assert.False(config.IsSendingEnabled);
    }

    /// <summary>
    /// 事件选择器默认为空，表示所有事件都进这个发件箱
    /// </summary>
    [Fact]
    public void Selector_ByDefault_IsNull()
    {
        var config = new OutboxConfig("Default");

        Assert.Null(config.Selector);
    }

    /// <summary>
    /// 事件选择器按事件类型做筛选
    /// </summary>
    [Fact]
    public void Selector_WhenAssigned_FiltersByEventType()
    {
        var config = new OutboxConfig("Default")
        {
            Selector = type => type == typeof(SampleEvent)
        };

        Assert.NotNull(config.Selector);
        Assert.True(config.Selector(typeof(SampleEvent)));
        Assert.False(config.Selector(typeof(AnotherSampleEvent)));
    }

    /// <summary>
    /// 实现类型默认为空，由具体事件盒实现包填充
    /// </summary>
    [Fact]
    public void ImplementationType_ByDefault_IsNull()
    {
        var config = new OutboxConfig("Default");

        Assert.Null(config.ImplementationType);
    }

    /// <summary>
    /// 实现类型可写入并读回
    /// </summary>
    [Fact]
    public void ImplementationType_WhenAssigned_IsReadBack()
    {
        var config = new OutboxConfig("Default")
        {
            ImplementationType = typeof(InMemoryEventOutbox)
        };

        Assert.Same(typeof(InMemoryEventOutbox), config.ImplementationType);
    }

    /// <summary>
    /// 发件箱配置不含收件箱侧的处理器选择器
    /// </summary>
    /// <remarks>
    /// 发件侧不解析处理器，出现同名成员意味着两类配置被误合并。
    /// </remarks>
    [Fact]
    public void OutboxConfig_HasNoHandlerSelector()
    {
        Assert.Null(typeof(OutboxConfig).GetProperty("HandlerSelector"));
        Assert.Null(typeof(OutboxConfig).GetProperty("EventSelector"));
    }
}
