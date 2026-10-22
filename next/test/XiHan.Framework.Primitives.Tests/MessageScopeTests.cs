// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace XiHan.Framework.Primitives.Tests;

/// <summary>
/// 消息作用域：<c>key.L()</c> 按当前作用域解析，<see cref="Error.Message"/> 按进程默认解析，都没有时退回键与参数的描述。
/// 本类会设置进程默认作用域，所以把依赖默认作用域的断言集中在这里并在结束时清除。
/// </summary>
public sealed class MessageScopeTests
{
    /// <summary>
    /// 两种语言的字典解析器：区域缺省为 en-US。
    /// </summary>
    private sealed class DictionaryResolver : IMessageResolver
    {
        /// <summary>
        /// 区域 → 键 → 模板。
        /// </summary>
        private static readonly Dictionary<string, Dictionary<string, string>> _templates = new(StringComparer.OrdinalIgnoreCase)
        {
            ["en-US"] = new(StringComparer.Ordinal) { ["orders.order.not_found"] = "Order '{id}' was not found.", ["orders.order.total"] = "{0} orders, {1:N2} in total." },
            ["zh-CN"] = new(StringComparer.Ordinal) { ["orders.order.not_found"] = "订单 '{id}' 不存在。" },
        };

        /// <inheritdoc />
        public bool TryResolve(string key, string? locale, IReadOnlyDictionary<string, object?>? arguments, [NotNullWhen(true)] out string? text)
        {
            locale ??= "en-US";
            if (_templates.TryGetValue(locale, out var templates) && templates.TryGetValue(key, out var template))
            {
                return MessageTemplate.TryFormat(template, arguments, CultureInfo.GetCultureInfo(locale), out text);
            }

            text = null;
            return false;
        }
    }

    [Fact]
    public void L_UsesCurrentScope_AndFallsBackToDescribe()
    {
        MessageScope.SetDefault(null, null);
        try
        {
            Assert.Null(MessageScope.Current);
            Assert.Equal("orders.order.not_found (id=42)", "orders.order.not_found".L(("id", 42)));

            using (MessageScope.Use(new DictionaryResolver(), "zh-CN"))
            {
                Assert.Equal("订单 '42' 不存在。", "orders.order.not_found".L(("id", 42)));
                Assert.Equal("orders.order.total (0=3, 1=1234.5)", "orders.order.total".L(3, 1234.5m));

                // 嵌套只换区域，沿用解析器；释放后恢复外层
                using (MessageScope.Use(null, "en-US"))
                {
                    Assert.Equal("Order '42' was not found.", "orders.order.not_found".L(MessageArguments.Of(("id", 42))));
                    Assert.Equal("3 orders, 1,234.50 in total.", "orders.order.total".L(3, 1234.5m));
                }

                Assert.Equal("zh-CN", MessageScope.Current!.Locale);
                Assert.Equal("orders.unknown", "orders.unknown".L());
            }

            Assert.Null(MessageScope.Current);
        }
        finally
        {
            MessageScope.SetDefault(null, null);
        }
    }

    [Fact]
    public async Task Scope_FlowsToChildTasks_AndDefaultRendersErrorMessage()
    {
        try
        {
            MessageScope.SetDefault(new DictionaryResolver(), "en-US");
            var error = Error.NotFound("ORDERS_NOTFOUND_001", "orders.order.not_found", MessageArguments.Of(("id", 42)));

            // Error.Message 按进程默认区域渲染，不受调用方区域作用域影响
            Assert.Equal("Order '42' was not found.", error.Message);
            using (MessageScope.Use(null, "zh-CN"))
            {
                Assert.Equal("Order '42' was not found.", error.Message);
                Assert.Equal("订单 '42' 不存在。", "orders.order.not_found".L(("id", 42)));
                Assert.Equal("订单 '42' 不存在。", await Task.Run(static () => "orders.order.not_found".L(("id", 42))));
            }

            Assert.Equal("[ORDERS_NOTFOUND_001] Order '42' was not found.", error.ToString());
            Assert.Equal("orders.unknown (id=1)", Error.Literal("ORDERS_NOTFOUND_001", "x").WithCode("ORDERS_NOTFOUND_002").Text is null ? "" : "orders.unknown".L(("id", 1)));
        }
        finally
        {
            MessageScope.SetDefault(null, null);
        }
    }

    [Fact]
    public void Dispose_IsIdempotent_AndOutOfOrderReleaseKeepsOthers()
    {
        MessageScope.SetDefault(null, null);
        var outer = MessageScope.Use(new DictionaryResolver(), "en-US");
        var inner = MessageScope.Use(null, "zh-CN");

        // 先释放外层：内层仍是当前作用域
        outer.Dispose();
        Assert.Same(inner, MessageScope.Current);
        inner.Dispose();
        inner.Dispose();
        Assert.Null(MessageScope.Current);
    }
}
