// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Localization.InMemory.Tests;

/// <summary>
/// 内存本地化特性：装载原语层与内核目录，启动后安装进程默认作用域与调用链作用域。
/// </summary>
public sealed class InMemoryLocalizationFeatureTests
{
    /// <summary>
    /// 业务目录。
    /// </summary>
    private static readonly MessageCatalog _orders = new("orders", new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
        ["en-US"] = new Dictionary<string, string> { ["orders.order.not_found"] = "Order '{id}' was not found." },
        ["zh-CN"] = new Dictionary<string, string> { ["orders.order.not_found"] = "订单 '{id}' 不存在。" },
        ["pt-BR"] = new Dictionary<string, string> { ["orders.order.not_found"] = "Pedido '{id}' não encontrado." },
    });

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
