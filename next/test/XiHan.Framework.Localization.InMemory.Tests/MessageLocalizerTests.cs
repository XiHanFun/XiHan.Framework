// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Localization.InMemory.Tests;

/// <summary>
/// 本地化器的回退链、文化格式化、错误翻译、上下文区域与特性装配（含 <c>key.L()</c> 的环境作用域）。
/// </summary>
public sealed class MessageLocalizerTests
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
    /// 用给定目录与选项创建本地化器。
    /// </summary>
    private static MessageLocalizer Localizer(LocalizationOptions? options = null, params MessageCatalog[] catalogs)
        => new(catalogs, options ?? new LocalizationOptions());

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
        // 非法区域按默认区域处理
        Assert.Equal("Order '{id}' was not found.", localizer.Find("orders.order.not_found", "???").ValueOrThrow());
        Assert.False(localizer.Find("orders.missing", "en-US").HasValue);

        // 默认区域改成 zh-CN 后，没有中文的键最后仍回退到源语言 en-US
        var chineseDefault = Localizer(new LocalizationOptions { DefaultLocale = "zh-CN" }, _orders);
        Assert.Equal("Not the owner.", chineseDefault.Find("orders.order.denied", "ko-KR").ValueOrThrow());
    }

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
    }

    [Fact]
    public void Localize_FallsBackToDescription_WhenMissingOrIncomplete()
    {
        var localizer = Localizer(null, _orders);

        Assert.Equal("orders.order.not_found", localizer.Localize("orders.order.not_found", "zh-CN", null));
        Assert.Equal("orders.missing (id=1)", localizer.Localize("orders.missing", "en-US", MessageArguments.Of(("id", 1))));
        Assert.False(localizer.TryResolve("orders.missing", null, null, out _));
        Assert.True(localizer.TryResolve("orders.order.denied", null, null, out var text));
        Assert.Equal("Not the owner.", text);
    }

    [Fact]
    public void Localize_Error_UsesKeyAndDetails_OrLiteralText()
    {
        var localizer = Localizer(null, _orders);
        var keyed = Error.NotFound("ORDERS_NOTFOUND_001", "orders.order.not_found", MessageArguments.Of(("id", 42)));

        Assert.Equal("订单 '42' 不存在。", localizer.Localize(keyed, "zh-CN"));
        Assert.Equal("Order '42' was not found.", localizer.Localize(keyed, "ja-JP"));
        Assert.Equal("Raw text.", localizer.Localize(Error.Literal("ORDERS_NOTFOUND_001", "Raw text."), "zh-CN"));
        Assert.Equal("orders.unknown.key", localizer.Localize(Error.NotFound("ORDERS_NOTFOUND_001", "orders.unknown.key"), "zh-CN"));
    }

    [Fact]
    public void Localize_WithContext_ReadsLocaleBaggage()
    {
        var localizer = Localizer(null, _orders);
        var chinese = TestInvocationContext.Create(baggage: new Dictionary<string, string> { [BaggageKeys.Locale] = "zh-CN" });

        Assert.Equal("订单 '7' 不存在。", localizer.Localize("orders.order.not_found", chinese, MessageArguments.Of(("id", 7))));
        Assert.Equal("Order '7' was not found.", localizer.Localize("orders.order.not_found", TestInvocationContext.Create(), MessageArguments.Of(("id", 7))));
        Assert.Equal("订单 '7' 不存在。", localizer.Localize(Error.NotFound("ORDERS_NOTFOUND_001", "orders.order.not_found", MessageArguments.Of(("id", 7))), chinese));
    }

    [Fact]
    public void FirstRegisteredCatalog_WinsForTheSameKey()
    {
        Assert.Equal("Not the owner.", Localizer(null, _orders, _override).Find("orders.order.denied", "en-US").ValueOrThrow());
        Assert.Equal("Overridden.", Localizer(null, _override, _orders).Find("orders.order.denied", "en-US").ValueOrThrow());
    }

    [Fact]
    public async Task Feature_RegistersKernelCatalogs_AndInstallsScopes()
    {
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryLocalizationFeature>(),
            [new("XiHan:Localization:DefaultLocale", "zh-CN")],
            services => services.AddMessageCatalog(_orders));

        var localizer = host.Services.GetRequiredService<IMessageLocalizer>();
        Assert.Same(localizer, host.Services.GetRequiredService<IMessageResolver>());
        Assert.Equal("zh-CN", localizer.Options.DefaultLocale);
        Assert.Equal("应用处于 Stopped 状态，暂不接收请求。", localizer.Localize(KernelMessages.NotServing, "zh-CN", MessageArguments.Of(("state", "Stopped"))));
        Assert.Equal("The manifest is empty.", localizer.Localize(KernelMessages.ManifestEmpty, "en-US", null));
        Assert.Equal(3, host.Services.GetServices<MessageCatalog>().Count());

        // 启动后进程默认作用域就绪：key.L() 与 Error.Message 按默认区域 zh-CN 渲染
        Assert.Equal("清单为空。", KernelMessages.ManifestEmpty.L());
        Assert.Equal("订单 '9' 不存在。", Error.NotFound("ORDERS_NOTFOUND_001", "orders.order.not_found", MessageArguments.Of(("id", 9))).Message);

        // 调用链里按 Baggage 的区域：显式 pt-BR 覆盖默认 zh-CN，扩展方法 context.Localize 同理
        string? inside = null;
        string? viaContext = null;
        var outcome = await host.InvokeAsync(
            context =>
            {
                inside = "orders.order.not_found".L(("id", 5));
                viaContext = context.Localize("orders.order.not_found", MessageArguments.Of(("id", 5)));
                return ValueTask.CompletedTask;
            },
            baggage: new Dictionary<string, string> { [BaggageKeys.Locale] = "pt-BR" });

        Assert.True(outcome.IsSuccess);
        Assert.Equal("Pedido '5' não encontrado.", inside);
        Assert.Equal("Pedido '5' não encontrado.", viaContext);
        Assert.Equal("清单为空。", KernelMessages.ManifestEmpty.L());
    }
}
