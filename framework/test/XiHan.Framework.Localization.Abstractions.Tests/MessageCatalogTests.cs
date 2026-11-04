// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Localization.Abstractions.Locales;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Localization.Abstractions.Tests;

/// <summary>
/// 消息目录：按区域分组装载、语言文件解析、嵌入资源装载，以及同名注册的合并与冲突
/// </summary>
public sealed class MessageCatalogTests
{
    /// <summary>
    /// 两种语言的目录
    /// </summary>
    /// <remarks>
    /// 区域名故意用小写验证规范化
    /// </remarks>
    private static MessageCatalog Orders() => new("orders", new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
        ["en-US"] = new Dictionary<string, string> { ["orders.order.not_found"] = "Order '{id}' was not found.", ["orders.order.denied"] = "Not the owner." },
        ["zh-cn"] = new Dictionary<string, string> { ["orders.order.not_found"] = "订单 '{id}' 不存在。" },
    });

    [Fact]
    public void Constructor_IndexesKeysByLocale()
    {
        var catalog = Orders();

        Assert.Equal("orders", catalog.Name);
        Assert.Equal(2, catalog.Count);
        Assert.Equal(["en-US", "zh-CN"], catalog.Locales);
        Assert.Equal("订单 '{id}' 不存在。", catalog.Find("orders.order.not_found", "ZH-CN").ValueOrThrow());
        Assert.False(catalog.Find("orders.order.denied", "zh-CN").HasValue);
        Assert.False(catalog.Find("orders.missing", "en-US").HasValue);
        Assert.Equal(["en-US", "zh-CN"], catalog.LocalesOf("orders.order.not_found").OrderBy(static l => l, StringComparer.Ordinal));
        Assert.Empty(catalog.LocalesOf("orders.missing"));
    }

    [Fact]
    public void Constructor_RejectsInvalidLocaleOrKey()
    {
        Assert.Equal(LocalizationErrors.InvalidCatalog, Assert.Throws<FrameworkException>(static () => new MessageCatalog("bad", new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["not a locale"] = new Dictionary<string, string>(),
        })).Code);
        Assert.Equal(LocalizationErrors.InvalidCatalog, Assert.Throws<FrameworkException>(static () => new MessageCatalog("bad", new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en-US"] = new Dictionary<string, string> { ["Not a key"] = "x" },
        })).Code);
        Assert.Equal(LocalizationErrors.InvalidCatalog, Assert.Throws<FrameworkException>(static () => new MessageCatalog("bad", new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en-US"] = new Dictionary<string, string>(),
            ["EN-us"] = new Dictionary<string, string>(),
        })).Code);
        Assert.Throws<ArgumentException>(static () => new MessageCatalog(" ", new Dictionary<string, IReadOnlyDictionary<string, string>>()));
    }

    [Fact]
    public void ParseLocaleFile_ReadsFlatObject_AndRejectsInvalidDocuments()
    {
        var parsed = MessageCatalog.ParseLocaleFile("orders", "en-US", Encoding.UTF8.GetBytes("""{ "orders.order.not_found": "Order '{id}' was not found." }"""));

        Assert.Equal("Order '{id}' was not found.", parsed["orders.order.not_found"]);
        Assert.Equal(LocalizationErrors.InvalidCatalog, Assert.Throws<FrameworkException>(static () => MessageCatalog.ParseLocaleFile("bad", "en-US", Encoding.UTF8.GetBytes("not json"))).Code);
        Assert.Equal(LocalizationErrors.InvalidCatalog, Assert.Throws<FrameworkException>(static () => MessageCatalog.ParseLocaleFile("bad", "en-US", Encoding.UTF8.GetBytes("null"))).Code);
        Assert.Equal(LocalizationErrors.InvalidCatalog, Assert.Throws<FrameworkException>(static () => MessageCatalog.ParseLocaleFile("bad", "en-US", Encoding.UTF8.GetBytes("""{ "bad key": "x" }"""))).Code);
        Assert.Equal(LocalizationErrors.InvalidCatalog, Assert.Throws<FrameworkException>(static () => MessageCatalog.ParseLocaleFile("bad", "en-US", Encoding.UTF8.GetBytes("""{ "orders.x.y": null }"""))).Code);
    }

    [Fact]
    public void FromEmbeddedResources_LoadsOneFilePerLocale()
    {
        var catalog = MessageCatalog.FromEmbeddedResources("primitives", typeof(Error));

        Assert.Equal(SupportedLocales.All.OrderBy(static l => l, StringComparer.Ordinal), catalog.Locales.OrderBy(static l => l, StringComparer.Ordinal));
        Assert.Contains(ManifestMessages.IsolationRequired, catalog.Keys);
        Assert.Equal("Plugin manifest '{id}' must declare 'isolation'.", catalog.Find(ManifestMessages.IsolationRequired, "en-US").ValueOrThrow());
        Assert.Equal("插件清单 '{id}' 必须声明 'isolation'。", catalog.Find(ManifestMessages.IsolationRequired, "zh-CN").ValueOrThrow());
        Assert.All(catalog.Keys, key => Assert.Equal(SupportedLocales.All.Count, catalog.LocalesOf(key).Count));

        // 本测试程序集没有嵌入语言文件
        Assert.Equal(LocalizationErrors.InvalidCatalog, Assert.Throws<FrameworkException>(static () => MessageCatalog.FromEmbeddedResources("tests", typeof(MessageCatalogTests))).Code);
    }

    [Fact]
    public void AddMessageCatalog_MergesSameNamedCatalogs_AndRejectsDisagreeingTemplates()
    {
        var services = new ServiceCollection();
        var first = Orders();
        var other = new MessageCatalog("shipments", new Dictionary<string, IReadOnlyDictionary<string, string>>());

        // 同样的内容（多个 Provider 都带同一个抽象包的目录）与它的子集：什么都不变，仍是先注册的那份
        var subset = new MessageCatalog("orders", new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en-US"] = new Dictionary<string, string> { ["orders.order.not_found"] = "Order '{id}' was not found." },
        });
        services.AddMessageCatalog(first).AddMessageCatalog(other).AddMessageCatalog(Orders()).AddMessageCatalog(subset);
        var registered = services.BuildServiceProvider().GetServices<MessageCatalog>().ToList();
        Assert.Equal(2, registered.Count);
        Assert.Same(first, registered[0]);
        Assert.Same(other, registered[1]);

        // 互为补充（插件带两种语言、宿主包带更多）：区域与键取并集，留在先注册的位置，不丢弃后来者
        services.AddMessageCatalog(new MessageCatalog("orders", new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["zh-CN"] = new Dictionary<string, string> { ["orders.order.not_found"] = "订单 '{id}' 不存在。", ["orders.order.denied"] = "不是所有者。" },
            ["ja-JP"] = new Dictionary<string, string> { ["orders.order.not_found"] = "注文 '{id}' が見つかりません。" },
        }));
        registered = services.BuildServiceProvider().GetServices<MessageCatalog>().ToList();
        Assert.Equal(["orders", "shipments"], registered.Select(static c => c.Name));
        var merged = registered[0];
        Assert.Equal(["en-US", "zh-CN", "ja-JP"], merged.Locales);
        Assert.Equal("Not the owner.", merged.Find("orders.order.denied", "en-US").ValueOrThrow());
        Assert.Equal("不是所有者。", merged.Find("orders.order.denied", "zh-CN").ValueOrThrow());
        Assert.Equal("注文 '{id}' が見つかりません。", merged.Find("orders.order.not_found", "ja-JP").ValueOrThrow());

        // 同一键同一区域的模板不同：注册时冲突，已注册的目录不变
        var thrown = Assert.Throws<FrameworkException>(() => services.AddMessageCatalog(new MessageCatalog("orders", new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en-US"] = new Dictionary<string, string> { ["orders.order.not_found"] = "Order '{id}' is missing." },
        })));
        Assert.Equal(LocalizationErrors.DuplicateCatalog, thrown.Code);
        Assert.Contains("'orders.order.not_found'", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("'en-US'", thrown.Message, StringComparison.Ordinal);
        Assert.Same(merged, services.BuildServiceProvider().GetServices<MessageCatalog>().First());
    }
}
