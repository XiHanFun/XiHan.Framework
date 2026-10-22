// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.InMemory;
using XiHan.Framework.Persistence.InMemory;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Localization.Database.Tests;

/// <summary>
/// 数据库 Provider 特有的行为：翻译优先于目录、重载与周期刷新、批量写入与删除、上限、顶替内存实现、选项解析。
/// </summary>
public sealed class DatabaseLocalizationTests
{
    /// <summary>
    /// 业务目录。
    /// </summary>
    private static readonly MessageCatalog _orders = new("orders", new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
        ["en-US"] = new Dictionary<string, string> { ["orders.order.not_found"] = "Order '{id}' was not found." },
        ["zh-CN"] = new Dictionary<string, string> { ["orders.order.not_found"] = "订单 '{id}' 不存在。" },
    });

    /// <summary>
    /// 启动装配了内存持久化与数据库本地化的宿主。
    /// </summary>
    private static Task<TestHost> StartAsync(params KeyValuePair<string, string?>[] configuration)
        => TestHost.StartAsync(
            static b => b.UseFeature<InMemoryPersistenceFeature>().UseFeature<InMemoryLocalizationFeature>().UseFeature<DatabaseLocalizationFeature>(),
            configuration,
            static services => services.AddMessageCatalog(_orders));

    [Fact]
    public async Task Translations_OverrideCatalogs_AfterReload_AndRemovalFallsBack()
    {
        await using var host = await StartAsync(new KeyValuePair<string, string?>("XiHan:Localization:Database:RefreshIntervalSeconds", "0"));
        var localizer = host.Services.GetRequiredService<IMessageLocalizer>();
        var loader = host.Services.GetRequiredService<MessageTranslationLoader>();
        // 显式 Provider 顶替默认的内存本地化
        Assert.IsType<DatabaseMessageLocalizer>(localizer);
        Assert.Same(localizer, host.Services.GetRequiredService<IMessageResolver>());
        Assert.Equal(["localization.resources"], host.App.Graph["xihan.localization.database"].SelectedCapabilities);
        Assert.Equal("订单 '1' 不存在。", localizer.Localize("orders.order.not_found", "zh-CN", MessageArguments.Of(("id", 1))));

        var written = await host.InvokeAsync(async context =>
        {
            var store = context.Services.GetRequiredService<MessageTranslationStore>();
            var results = await store.UpsertAsync(
            [
                new MessageTranslationEntry("orders.order.not_found", "zh-cn", "订单 '{id}' 找不到。"),
                new MessageTranslationEntry("orders.order.not_found", "zh-TW", "訂單 '{id}' 找不到。"),
                new MessageTranslationEntry("orders.order.not_found", "zh-CN", "订单 '{id}' 真的找不到。"),
            ], context.CancellationToken);
            Assert.All(results, static r => Assert.True(r.Outcome.IsSuccess));
            Assert.Equal(["zh-CN", "zh-TW", "zh-CN"], results.Select(static r => r.Key.Locale));
            Assert.Equal(2, (await store.ListAsync(null, 0, 10, context.CancellationToken)).Count);
            Assert.Single(await store.ListAsync("zh-TW", 0, 10, context.CancellationToken));
        });
        Assert.True(written.IsSuccess);

        // 重载前仍是目录文案；重载后数据库优先，同一批里后者覆盖前者
        Assert.Equal("订单 '1' 不存在。", localizer.Localize("orders.order.not_found", "zh-CN", MessageArguments.Of(("id", 1))));
        Assert.True((await loader.ReloadAsync(CancellationToken.None)).IsSuccess);
        Assert.NotNull(loader.LastLoadedAt);
        Assert.Equal(2, ((DatabaseMessageLocalizer)localizer).TranslationCount);
        Assert.Equal("订单 '1' 真的找不到。", localizer.Localize("orders.order.not_found", "zh-CN", MessageArguments.Of(("id", 1))));
        Assert.Equal("訂單 '1' 找不到。", localizer.Localize("orders.order.not_found", "zh-TW", MessageArguments.Of(("id", 1))));
        // 数据库没有的区域照常回退到目录
        Assert.Equal("Order '1' was not found.", localizer.Localize("orders.order.not_found", "ja-JP", MessageArguments.Of(("id", 1))));
        // 调用链里按 Baggage 区域也走数据库快照
        string? inside = null;
        await host.InvokeAsync(_ =>
        {
            inside = "orders.order.not_found".L(("id", 2));
            return ValueTask.CompletedTask;
        }, baggage: new Dictionary<string, string> { [BaggageKeys.Locale] = "zh-CN" });
        Assert.Equal("订单 '2' 真的找不到。", inside);

        var removed = await host.InvokeAsync(async context =>
        {
            var store = context.Services.GetRequiredService<MessageTranslationStore>();
            var results = await store.RemoveAsync([new MessageTranslationKey("orders.order.not_found", "zh-CN"), new MessageTranslationKey("orders.order.not_found", "ko-KR")], context.CancellationToken);
            Assert.True(results[0].Outcome.IsSuccess);
            Assert.Equal(LocalizationErrors.TranslationNotFound, results[1].Outcome.Error?.Code);
            Assert.Equal("数据库里没有翻译 'orders.order.not_found'（ko-KR）。", context.Services.GetRequiredService<IMessageLocalizer>().Localize(results[1].Outcome.Error!, "zh-CN"));
        }, baggage: new Dictionary<string, string> { [BaggageKeys.Locale] = "zh-CN" });
        Assert.True(removed.IsSuccess);
        await loader.ReloadAsync(CancellationToken.None);
        Assert.Equal("订单 '1' 不存在。", localizer.Localize("orders.order.not_found", "zh-CN", MessageArguments.Of(("id", 1))));
        Assert.Equal(1, ((DatabaseMessageLocalizer)localizer).TranslationCount);
    }

    [Fact]
    public async Task BackgroundRefresh_PicksUpChanges_AndHonoursTheLimit()
    {
        await using var host = await StartAsync(
            new KeyValuePair<string, string?>("XiHan:Localization:Database:RefreshIntervalSeconds", "0.05"),
            new KeyValuePair<string, string?>("XiHan:Localization:Database:MaxTranslations", "2"));
        var localizer = host.Services.GetRequiredService<DatabaseMessageLocalizer>();

        Assert.True((await host.InvokeAsync(async context =>
        {
            var store = context.Services.GetRequiredService<MessageTranslationStore>();
            await store.UpsertAsync(
            [
                new MessageTranslationEntry("orders.order.not_found", "zh-CN", "一"),
                new MessageTranslationEntry("orders.order.not_found", "zh-TW", "二"),
                new MessageTranslationEntry("orders.order.not_found", "ja-JP", "三"),
            ], context.CancellationToken);
        })).IsSuccess);

        // 不显式重载，周期刷新自己装载；上限 2 只装前两条
        await WaitUntilAsync(() => localizer.TranslationCount == 2);
        Assert.Equal("一", localizer.Find("orders.order.not_found", "zh-CN").ValueOrThrow());
    }

    [Fact]
    public void Entries_AndOptions_Validate()
    {
        Assert.Throws<ArgumentException>(static () => new MessageTranslationEntry("Bad.Key", "zh-CN", "x"));
        Assert.Throws<ArgumentException>(static () => new MessageTranslationEntry("orders.ok", "??", "x"));
        Assert.Throws<ArgumentException>(static () => new MessageTranslationEntry("orders.ok", "zh-CN", " "));
        Assert.Equal("zh-Hant-TW", new MessageTranslationKey("orders.ok", "ZH-hant-tw").Locale);

        var options = DatabaseLocalizationOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["XiHan:Localization:Database:RefreshIntervalSeconds"] = "0",
            ["XiHan:Localization:Database:MaxTranslations"] = "5",
        }).Build());
        Assert.Equal((null, 5), (options.RefreshInterval, options.MaxTranslations));
        Assert.Equal(TimeSpan.FromMinutes(5), new DatabaseLocalizationOptions().RefreshInterval);
        Assert.Throws<ArgumentOutOfRangeException>(static () => new DatabaseLocalizationOptions { MaxTranslations = 0 });
        Assert.Throws<ArgumentException>(static () => DatabaseLocalizationOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["XiHan:Localization:Database:RefreshIntervalSeconds"] = "-1" }).Build()));
    }

    /// <summary>
    /// 轮询直到条件成立，最多 10 秒。
    /// </summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("The condition did not become true in time.");
            }

            await Task.Delay(20);
        }
    }
}
