// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.EventBus.Abstractions.Distributed;

namespace XiHan.Framework.EventBus.Abstractions.Tests.Distributed;

/// <summary>
/// 收件箱配置测试
/// </summary>
/// <remarks>
/// 收件箱名称是配置字典的键，一旦为空会让整张字典的定位失效，因此构造时即校验；
/// 数据库名称同理，它决定收件箱表落在哪个库上，写入空值会把事件写丢，所以在 setter 上校验。
/// 两处校验都走 Guard，抛的是 <see cref="ArgumentException"/> 而非 <see cref="ArgumentNullException"/>。
/// </remarks>
public class InboxConfigTests
{
    /// <summary>
    /// 构造函数写入的名称原样暴露
    /// </summary>
    [Fact]
    public void Ctor_WithName_ExposesSameName()
    {
        var config = new InboxConfig("Default");

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
            _ = new InboxConfig(name!);
        });

        Assert.Equal("name", exception.ParamName);
    }

    /// <summary>
    /// 名称只读，构造后不可改写
    /// </summary>
    [Fact]
    public void Name_IsReadOnly()
    {
        var property = typeof(InboxConfig).GetProperty(nameof(InboxConfig.Name));

        Assert.NotNull(property);
        Assert.False(property.CanWrite);
    }

    /// <summary>
    /// 数据库名称写入后可读回
    /// </summary>
    [Fact]
    public void DatabaseName_WhenAssigned_IsReadBack()
    {
        var config = new InboxConfig("Default")
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
        var config = new InboxConfig("Default");

        var exception = Assert.Throws<ArgumentException>(() =>
        {
            config.DatabaseName = databaseName!;
        });

        Assert.Equal(nameof(InboxConfig.DatabaseName), exception.ParamName);
    }

    /// <summary>
    /// 赋值失败后不会污染已有的数据库名称
    /// </summary>
    [Fact]
    public void DatabaseName_WhenAssignmentFails_KeepsPreviousValue()
    {
        var config = new InboxConfig("Default")
        {
            DatabaseName = "EventStore"
        };

        Assert.Throws<ArgumentException>(() =>
        {
            config.DatabaseName = "  ";
        });

        Assert.Equal("EventStore", config.DatabaseName);
    }

    /// <summary>
    /// 默认启用入站事件处理
    /// </summary>
    [Fact]
    public void IsProcessingEnabled_ByDefault_IsTrue()
    {
        var config = new InboxConfig("Default");

        Assert.True(config.IsProcessingEnabled);
    }

    /// <summary>
    /// 可关闭入站事件处理
    /// </summary>
    [Fact]
    public void IsProcessingEnabled_CanBeDisabled()
    {
        var config = new InboxConfig("Default")
        {
            IsProcessingEnabled = false
        };

        Assert.False(config.IsProcessingEnabled);
    }

    /// <summary>
    /// 未设置选择器时按「不过滤」语义处理，两个选择器默认都为空
    /// </summary>
    [Fact]
    public void Selectors_ByDefault_AreNull()
    {
        var config = new InboxConfig("Default");

        Assert.Null(config.EventSelector);
        Assert.Null(config.HandlerSelector);
    }

    /// <summary>
    /// 事件选择器按事件类型做筛选
    /// </summary>
    [Fact]
    public void EventSelector_WhenAssigned_FiltersByEventType()
    {
        var config = new InboxConfig("Default")
        {
            EventSelector = type => type == typeof(SampleEvent)
        };

        Assert.NotNull(config.EventSelector);
        Assert.True(config.EventSelector(typeof(SampleEvent)));
        Assert.False(config.EventSelector(typeof(AnotherSampleEvent)));
    }

    /// <summary>
    /// 处理器选择器按处理器类型做筛选，与事件选择器相互独立
    /// </summary>
    [Fact]
    public void HandlerSelector_WhenAssigned_FiltersByHandlerType()
    {
        var config = new InboxConfig("Default")
        {
            HandlerSelector = type => type == typeof(RecordingLocalEventHandler)
        };

        Assert.NotNull(config.HandlerSelector);
        Assert.True(config.HandlerSelector(typeof(RecordingLocalEventHandler)));
        Assert.False(config.HandlerSelector(typeof(MultiEventHandler)));
        Assert.Null(config.EventSelector);
    }

    /// <summary>
    /// 实现类型默认为空，由具体事件盒实现包填充
    /// </summary>
    [Fact]
    public void ImplementationType_ByDefault_IsNull()
    {
        var config = new InboxConfig("Default");

        Assert.Null(config.ImplementationType);
    }

    /// <summary>
    /// 实现类型可写入并读回
    /// </summary>
    [Fact]
    public void ImplementationType_WhenAssigned_IsReadBack()
    {
        var config = new InboxConfig("Default")
        {
            ImplementationType = typeof(InMemoryEventInbox)
        };

        Assert.Same(typeof(InMemoryEventInbox), config.ImplementationType);
    }
}
