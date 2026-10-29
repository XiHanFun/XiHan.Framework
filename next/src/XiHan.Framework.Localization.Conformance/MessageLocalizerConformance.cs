// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;
using Xunit;

namespace XiHan.Framework.Localization.Conformance;

/// <summary>
/// <see cref="IMessageLocalizer"/> 的一致性套件（RFC-0010）。
/// </summary>
/// <remarks>
/// 继承并实现 <see cref="CreateLocalizer"/>，全部用例随之执行；实现同时必须是 <see cref="IMessageResolver"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class MessageLocalizerConformance
{
    /// <summary>
    /// 业务目录：区域不齐，用来验证回退链与按区域格式化。
    /// </summary>
    private static readonly MessageCatalog _orders = new("orders", new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
        ["en-US"] = new Dictionary<string, string>
        {
            ["orders.order.not_found"] = "Order '{id}' was not found.",
            ["orders.order.total"] = "{0} orders, {1:N2} in total.",
            ["orders.order.denied"] = "Not the owner.",
        },
        ["zh-CN"] = new Dictionary<string, string> { ["orders.order.not_found"] = "订单 '{id}' 不存在。", ["orders.order.total"] = "共 {0} 个订单，合计 {1:N2}。" },
        ["pt-BR"] = new Dictionary<string, string> { ["orders.order.not_found"] = "Pedido '{id}' não encontrado." },
        ["de-DE"] = new Dictionary<string, string> { ["orders.order.total"] = "{0} Bestellungen, insgesamt {1:N2}." },
        ["zh-TW"] = new Dictionary<string, string> { ["orders.order.denied"] = "不是擁有者。" },
    });

    /// <summary>
    /// 与业务目录撞键的目录，用来验证注册顺序决定优先级。
    /// </summary>
    private static readonly MessageCatalog _override = new("override", new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
        ["en-US"] = new Dictionary<string, string> { ["orders.order.denied"] = "Overridden." },
    });

    /// <summary>
    /// 用给定目录（按注册顺序）与选项创建被测本地化器。
    /// </summary>
    protected abstract IMessageLocalizer CreateLocalizer(IEnumerable<MessageCatalog> catalogs, LocalizationOptions options);

    /// <summary>
    /// 用例 1：回退链为精确区域 → 同语言的启用区域 → 默认区域 → 源语言。
    /// </summary>
    /// <remarks>
    /// 非法区域按默认区域处理。
    /// </remarks>
    [Fact]
    public void Find_FallsBackExactThenLanguageThenDefaultThenSource()
    {
        var localizer = Localizer(null, _orders);

        Assert.Equal("订单 '{id}' 不存在。", localizer.Find("orders.order.not_found", "zh-cn").ValueOrThrow());
        // zh-TW 没有这条，同语言回退到 zh-CN
        Assert.Equal("订单 '{id}' 不存在。", localizer.Find("orders.order.not_found", "zh-TW").ValueOrThrow());
        // pt-PT 未启用，同语言回退到 pt-BR
        Assert.Equal("Pedido '{id}' não encontrado.", localizer.Find("orders.order.not_found", "pt-PT").ValueOrThrow());
        // ja-JP 没有也没有同语言，回退到默认 en-US
        Assert.Equal("Order '{id}' was not found.", localizer.Find("orders.order.not_found", "ja-JP").ValueOrThrow());
        Assert.Equal("Order '{id}' was not found.", localizer.Find("orders.order.not_found", "???").ValueOrThrow());
        Assert.False(localizer.Find("orders.missing", "en-US").HasValue);

        // 默认区域改成 zh-CN 后，没有中文的键最后仍回退到源语言 en-US
        var chineseDefault = Localizer(new LocalizationOptions { DefaultLocale = "zh-CN" }, _orders);
        Assert.Equal("Not the owner.", chineseDefault.Find("orders.order.denied", "ko-KR").ValueOrThrow());
    }

    /// <summary>
    /// 用例 2：按实际命中区域的文化格式化。
    /// </summary>
    [Fact]
    public void Localize_FormatsWithTheCultureOfTheResolvedLocale()
    {
        var localizer = Localizer(null, _orders);
        var totals = MessageArguments.Positional(3, 1234.5m);

        Assert.Equal("订单 '42' 不存在。", localizer.Localize("orders.order.not_found", "zh-CN", MessageArguments.Of(("id", 42))));
        Assert.Equal("3 Bestellungen, insgesamt 1.234,50.", localizer.Localize("orders.order.total", "de-DE", totals));
        Assert.Equal("共 3 个订单，合计 1,234.50。", localizer.Localize("orders.order.total", "zh-CN", totals));
        // fr-FR 没有模板，回退到 en-US 并按 en-US 文化格式化
        Assert.Equal("3 orders, 1,234.50 in total.", localizer.Localize("orders.order.total", "fr-FR", totals));
        var resolved = localizer.Resolve("orders.order.total", "fr-FR", totals).ValueOrThrow();
        Assert.Equal(("3 orders, 1,234.50 in total.", "en-US"), (resolved.Text, resolved.Locale));
    }

    /// <summary>
    /// 用例 3：没有模板或占位符缺值时退回键与参数的描述。
    /// </summary>
    /// <remarks>
    /// 解析器接口同样如此。
    /// </remarks>
    [Fact]
    public void Localize_FallsBackToDescription_WhenMissingOrIncomplete()
    {
        var localizer = Localizer(null, _orders);
        var resolver = Assert.IsAssignableFrom<IMessageResolver>(localizer);

        Assert.Equal("orders.order.not_found", localizer.Localize("orders.order.not_found", "zh-CN", null));
        Assert.Equal("orders.missing (id=1)", localizer.Localize("orders.missing", "en-US", MessageArguments.Of(("id", 1))));
        Assert.False(localizer.Resolve("orders.missing", "en-US", null).HasValue);
        Assert.False(resolver.TryResolve("orders.missing", null, null, out _));
        Assert.True(resolver.TryResolve("orders.order.denied", null, null, out var text));
        Assert.Equal("Not the owner.", text);
    }

    /// <summary>
    /// 用例 4：错误按键与细节翻译。
    /// </summary>
    /// <remarks>
    /// 自由文本错误原样返回；没有模板时返回键。
    /// </remarks>
    [Fact]
    public void Localize_Error_UsesKeyAndDetails_OrLiteralText()
    {
        var localizer = Localizer(null, _orders);
        var keyed = Error.NotFound("ORDERS_NOTFOUND_001", "orders.order.not_found", MessageArguments.Of(("id", 42)));

        Assert.Equal("订单 '42' 不存在。", localizer.Localize(keyed, "zh-CN"));
        Assert.Equal("Order '42' was not found.", localizer.Localize(keyed, "ja-JP"));
        Assert.Equal("zh-CN", localizer.Resolve(keyed, "zh-CN").ValueOrThrow().Locale);
        Assert.Equal("Raw text.", localizer.Localize(Error.Literal("ORDERS_NOTFOUND_001", "Raw text."), "zh-CN"));
        Assert.False(localizer.Resolve(Error.Literal("ORDERS_NOTFOUND_001", "Raw text."), "zh-CN").HasValue);
        Assert.Equal("orders.unknown.key", localizer.Localize(Error.NotFound("ORDERS_NOTFOUND_001", "orders.unknown.key"), "zh-CN"));
    }

    /// <summary>
    /// 用例 5：按调用上下文的 Baggage 区域翻译，缺失时用默认区域。
    /// </summary>
    [Fact]
    public void Localize_WithContext_ReadsLocaleBaggage()
    {
        var localizer = Localizer(null, _orders);
        var chinese = TestInvocationContext.Create(baggage: new Dictionary<string, string> { [BaggageKeys.Locale] = "zh-CN" });

        Assert.Equal("订单 '7' 不存在。", localizer.Localize("orders.order.not_found", chinese, MessageArguments.Of(("id", 7))));
        Assert.Equal("Order '7' was not found.", localizer.Localize("orders.order.not_found", TestInvocationContext.Create(), MessageArguments.Of(("id", 7))));
        Assert.Equal("订单 '7' 不存在。", localizer.Localize(Error.NotFound("ORDERS_NOTFOUND_001", "orders.order.not_found", MessageArguments.Of(("id", 7))), chinese));
    }

    /// <summary>
    /// 用例 6：同一键在多个目录里时，先注册的目录优先。
    /// </summary>
    [Fact]
    public void FirstRegisteredCatalog_WinsForTheSameKey()
    {
        Assert.Equal("Not the owner.", Localizer(null, _orders, _override).Find("orders.order.denied", "en-US").ValueOrThrow());
        Assert.Equal("Overridden.", Localizer(null, _override, _orders).Find("orders.order.denied", "en-US").ValueOrThrow());
    }

    /// <summary>
    /// 用给定目录与选项创建本地化器。
    /// </summary>
    private IMessageLocalizer Localizer(LocalizationOptions? options, params MessageCatalog[] catalogs) => CreateLocalizer(catalogs, options ?? new LocalizationOptions());
}
