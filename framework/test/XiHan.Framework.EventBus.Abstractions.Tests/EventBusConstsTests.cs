// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;

namespace XiHan.Framework.EventBus.Abstractions.Tests;

/// <summary>
/// 事件总线常量测试
/// </summary>
/// <remarks>
/// 关联标识符请求头名称会随消息一起跨进程传输，并作为事件盒扩展属性的键持久化，
/// 一旦改动就会造成新旧版本互相读不到关联标识，因此这里把字面量锁死。
/// </remarks>
public class EventBusConstsTests
{
    /// <summary>
    /// 关联标识符请求头名称锁定为 W3C 风格的自定义头
    /// </summary>
    [Fact]
    public void CorrelationIdHeaderName_IsPinned()
    {
        Assert.Equal("X-Correlation-Id", EventBusConsts.CorrelationIdHeaderName);
    }

    /// <summary>
    /// 常量以编译期字面量形式暴露，允许在特性参数与 switch 分支中使用
    /// </summary>
    [Fact]
    public void CorrelationIdHeaderName_IsCompileTimeConstant()
    {
        var field = typeof(EventBusConsts).GetField(
            nameof(EventBusConsts.CorrelationIdHeaderName),
            BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(field);
        Assert.True(field.IsLiteral);
        Assert.Equal(typeof(string), field.FieldType);
    }

    /// <summary>
    /// 常量宿主为静态类，不应被实例化或继承
    /// </summary>
    [Fact]
    public void EventBusConsts_IsStaticClass()
    {
        Assert.True(typeof(EventBusConsts).IsAbstract);
        Assert.True(typeof(EventBusConsts).IsSealed);
    }
}
