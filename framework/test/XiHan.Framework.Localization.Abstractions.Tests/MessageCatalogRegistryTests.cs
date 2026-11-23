// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Localization.Abstractions.Tests;

/// <summary>
/// 目录登记表的按归属登记与撤回
/// </summary>
/// <remarks>
/// 注册的目录在前，按归属登记的在后；一组全进或全不进，按归属一次撤回（ADR-0252）
/// </remarks>
public sealed class MessageCatalogRegistryTests
{
    /// <summary>
    /// 只有一种区域的目录
    /// </summary>
    private static MessageCatalog Catalog(string name, string locale, params (string Key, string Template)[] templates)
        => new(name, new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            [locale] = templates.ToDictionary(static t => t.Key, static t => t.Template, StringComparer.Ordinal),
        });

    [Fact]
    public void Add_AppendsAfterTheRegisteredCatalogs_AndRemoveOwnedByTakesTheWholeGroupAway()
    {
        var host = Catalog("orders", "en-US", ("orders.order.not_found", "Order not found."));
        var registry = new MessageCatalogRegistry([host]);
        var first = Catalog("echo", "en-US", ("echo.text.empty", "The text must not be empty."));
        var second = Catalog("echo.extra", "en-US", ("echo.text.long", "The text is too long."));

        registry.Add("acme.echo", [first, second]);

        Assert.Equal([host, first, second], registry.Catalogs);
        Assert.Equal(2, registry.RemoveOwnedBy("acme.echo"));
        Assert.Equal([host], registry.Catalogs);
        // 没有登记过的归属：什么都不做
        Assert.Equal(0, registry.RemoveOwnedBy("acme.echo"));
        // 撤回之后同一归属可以再登记
        registry.Add("acme.echo", [first]);
        Assert.Equal([host, first], registry.Catalogs);
    }

    [Fact]
    public void Catalogs_IsASnapshot()
    {
        var registry = new MessageCatalogRegistry([]);
        var before = registry.Catalogs;

        registry.Add("acme.echo", [Catalog("echo", "en-US", ("echo.text.empty", "Empty."))]);

        Assert.Empty(before);
        Assert.Single(registry.Catalogs);
    }

    [Fact]
    public void Add_SameNameCatalogs_AreCheckedButNotMerged()
    {
        // 宿主为插件的目录补了一种语言：同一键同一区域的模板一致，互为补充，各自留着
        var host = Catalog("echo", "zh-CN", ("echo.text.empty", "文本不能为空。"));
        var registry = new MessageCatalogRegistry([host]);
        var plugin = new MessageCatalog("echo", new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en-US"] = new Dictionary<string, string> { ["echo.text.empty"] = "The text must not be empty." },
            ["zh-CN"] = new Dictionary<string, string> { ["echo.text.empty"] = "文本不能为空。" },
        });

        registry.Add("acme.echo", [plugin]);
        Assert.Equal([host, plugin], registry.Catalogs);

        // 撤回只带走插件那份，宿主注册的留下
        registry.RemoveOwnedBy("acme.echo");
        Assert.Equal([host], registry.Catalogs);
    }

    [Fact]
    public void Add_ConflictingTemplate_RejectsTheWholeGroup()
    {
        var host = Catalog("echo", "en-US", ("echo.text.empty", "Text required."));
        var registry = new MessageCatalogRegistry([host]);
        var unrelated = Catalog("echo.extra", "en-US", ("echo.text.long", "Too long."));
        var conflicting = Catalog("echo", "en-US", ("echo.text.empty", "The text must not be empty."));

        // 冲突的在组里排第二：排在它前面的也不留下
        var thrown = Assert.Throws<FrameworkException>(() => registry.Add("acme.echo", [unrelated, conflicting]));

        Assert.Equal(LocalizationErrors.DuplicateCatalog, thrown.Code);
        Assert.Contains("'acme.echo'", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("'echo.text.empty'", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("'en-US'", thrown.Message, StringComparison.Ordinal);
        Assert.Equal([host], registry.Catalogs);
        Assert.Equal(0, registry.RemoveOwnedBy("acme.echo"));
    }

    [Fact]
    public void Add_ConflictWithinTheGroupOrWithAnotherOwner_IsRejected()
    {
        var registry = new MessageCatalogRegistry([]);
        registry.Add("acme.first", [Catalog("shared", "en-US", ("shared.text", "First."))]);

        Assert.Equal(LocalizationErrors.DuplicateCatalog, Assert.Throws<FrameworkException>(() => registry.Add("acme.second", [Catalog("shared", "en-US", ("shared.text", "Second."))])).Code);
        Assert.Equal(LocalizationErrors.DuplicateCatalog, Assert.Throws<FrameworkException>(() => registry.Add("acme.third", [Catalog("own", "en-US", ("own.text", "A.")), Catalog("own", "en-US", ("own.text", "B."))])).Code);
        Assert.Single(registry.Catalogs);
    }

    [Fact]
    public void Add_SameKeyInADifferentlyNamedCatalog_IsNotAConflict()
    {
        // 不同名目录里的同一键不冲突：查找时先找到的优先，宿主已有的在前
        var host = Catalog("kernel", "en-US", ("echo.text.empty", "Host text."));
        var registry = new MessageCatalogRegistry([host]);
        var plugin = Catalog("echo", "en-US", ("echo.text.empty", "Plugin text."));

        registry.Add("acme.echo", [plugin]);

        Assert.Equal([host, plugin], registry.Catalogs);
    }

    [Fact]
    public void Add_AnOwnerTwice_IsAProgrammingError_AndAnEmptyGroupDoesNothing()
    {
        var registry = new MessageCatalogRegistry([]);
        registry.Add("acme.echo", []);
        Assert.Empty(registry.Catalogs);

        registry.Add("acme.echo", [Catalog("echo", "en-US", ("echo.text.empty", "Empty."))]);
        Assert.Throws<InvalidOperationException>(() => registry.Add("acme.echo", [Catalog("echo.more", "en-US", ("echo.text.more", "More."))]));
        Assert.Single(registry.Catalogs);
        Assert.Throws<ArgumentException>(() => registry.Add(" ", []));
    }

    [Fact]
    public void Container_RegistersTheRegistry_WithOwnedCatalogsAfterTheRegisteredOnes()
    {
        var host = Catalog("orders", "en-US", ("orders.order.not_found", "Order not found."));
        var plugin = Catalog("echo", "en-US", ("echo.text.empty", "Empty."));
        var later = Catalog("shipments", "en-US", ("shipments.shipment.late", "Late."));
        var services = new ServiceCollection();

        // 按归属注册的排在注册的目录之后，即使先注册
        services.AddMessageCatalog("acme.echo", plugin).AddMessageCatalog(host).AddMessageCatalog(later);
        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<MessageCatalogRegistry>();

        Assert.Equal([host, later, plugin], registry.Catalogs);
        // 配置阶段按归属注册的同样可以撤回
        Assert.Equal(1, registry.RemoveOwnedBy("acme.echo"));
        Assert.Equal([host, later], registry.Catalogs);
        // 注册的目录本身仍在容器里，不含按归属注册的
        Assert.Equal([host, later], provider.GetServices<MessageCatalog>());
    }

    [Fact]
    public void Container_ConflictingOwnedCatalog_FailsWhenTheRegistryIsBuilt()
    {
        var services = new ServiceCollection();
        services.AddMessageCatalog(Catalog("echo", "en-US", ("echo.text.empty", "Host text.")));
        services.AddMessageCatalog("acme.echo", Catalog("echo", "en-US", ("echo.text.empty", "Plugin text.")));
        using var provider = services.BuildServiceProvider();

        Assert.Equal(LocalizationErrors.DuplicateCatalog, Assert.Throws<FrameworkException>(() => provider.GetRequiredService<MessageCatalogRegistry>()).Code);
    }
}
