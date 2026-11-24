// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Primitives.Tests.Manifests;

/// <summary>
/// 清单激活事件的文法（RFC-0002 解析规则 2 第 3 步，ADR-0276）
/// </summary>
public sealed class ActivationEventTests
{
    [Theory]
    [InlineData("onStartup")]
    [InlineData("onOperation:orders.create")]
    [InlineData("onOperation:order-lines.*")]
    [InlineData("onOperation:*")]
    [InlineData("onEvent:orders.created")]
    [InlineData("onEvent:*")]
    // 主题文法（RFC-0009）允许下划线：修之前 onEvent 与 onOperation 共用一个字符集，这两条写不进清单
    [InlineData("onEvent:order_events.*")]
    [InlineData("onEvent:a_b")]
    public void IsValid_AcceptsTheThreeForms(string value)
        => Assert.True(ActivationEvent.IsValid(value), value);

    [Theory]
    [InlineData("")]
    [InlineData("onBoot")]
    [InlineData("onStartup:x")]
    [InlineData("onOperation:")]
    [InlineData("onEvent:")]
    // 操作 ID 里没有下划线，放宽的只是 onEvent
    [InlineData("onOperation:order_lines.*")]
    [InlineData("onEvent:Order_Placed")]
    [InlineData("onEvent:a b")]
    [InlineData("onEvent:a_b\n")]
    [InlineData(" onStartup")]
    [InlineData("OnStartup")]
    public void IsValid_RejectsEverythingElse(string value)
        => Assert.False(ActivationEvent.IsValid(value), value);

    [Fact]
    public void IsValid_RejectsNull()
        => Assert.False(ActivationEvent.IsValid(null));
}
