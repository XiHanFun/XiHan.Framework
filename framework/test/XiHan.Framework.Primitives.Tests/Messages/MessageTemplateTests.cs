// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;

namespace XiHan.Framework.Primitives.Tests.Messages;

/// <summary>
/// 消息键文法、模板格式化与参数字典
/// </summary>
public sealed class MessageTemplateTests
{
    [Theory]
    [InlineData("kernel.manifest.empty", true)]
    [InlineData("persistence.entity.not_found", true)]
    [InlineData("a1.b2_c3.d", true)]
    [InlineData("kernel_core.empty", true)]
    [InlineData("tenant_members.list.description", true)]
    [InlineData("kernel", false)]
    [InlineData("Kernel.manifest.empty", false)]
    [InlineData("kernel.Manifest", false)]
    [InlineData("kernel..empty", false)]
    [InlineData("kernel.manifest.", false)]
    [InlineData("_tenant.list", false)]
    [InlineData("tenant-members.list.description", false)]
    [InlineData("", false)]
    public void IsValidKey_FollowsGrammar(string key, bool valid)
    {
        Assert.Equal(valid, MessageTemplate.IsValidKey(key));
        if (valid)
        {
            Assert.Equal(key, MessageTemplate.ValidateKey(key));
        }
        else
        {
            Assert.Throws<ArgumentException>(() => MessageTemplate.ValidateKey(key));
        }
    }

    [Fact]
    public void DomainOf_ReturnsFirstSegment()
    {
        Assert.Equal("security", MessageTemplate.DomainOf("security.token.invalid"));
        Assert.False(MessageTemplate.IsValidKey(null));
        Assert.Throws<ArgumentNullException>(static () => MessageTemplate.ValidateKey(null!));
    }

    [Fact]
    public void Format_SubstitutesNamedAndPositionalPlaceholders()
    {
        var named = MessageArguments.Of(("id", 42), ("name", "widget"));

        Assert.Equal("Order '42' (widget) {kept}.", MessageTemplate.Format("Order '{id}' ({name}) {{kept}}.", named, CultureInfo.InvariantCulture));
        Assert.Equal("3 orders, 1,234.50 in total.", MessageTemplate.Format("{0} orders, {1:N2} in total.", MessageArguments.Positional(3, 1234.5m), CultureInfo.InvariantCulture));
        Assert.Equal("No placeholders.", MessageTemplate.Format("No placeholders.", null, CultureInfo.InvariantCulture));
        Assert.Equal("Unclosed {brace", MessageTemplate.Format("Unclosed {brace", named, CultureInfo.InvariantCulture));
        Assert.Equal("{\"json\": 1}", MessageTemplate.Format("{\"json\": 1}", named, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Format_UsesOnlyPortableSpecifiersAndTheLocaleSymbols()
    {
        var arguments = MessageArguments.Of(("amount", 1234.5m), ("when", new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc)));

        // 日期时间不在值模型里，按不变文化的文本写出；.NET 的格式串不是可移植的说明符，按无格式（RFC-0010「模板」，ADR-0236）
        Assert.Equal("1,234.50 on 10/21/2026 00:00:00", MessageTemplate.Format("{amount:N2} on {when:yyyy-MM-dd}", arguments, CultureInfo.InvariantCulture));
        Assert.Equal("1.234,50", MessageTemplate.Format("{amount:N2}", arguments, CultureInfo.GetCultureInfo("de-DE")));

        // 区域只改变小数点与分组符，NaN 与无穷不随区域变化
        Assert.Equal("Infinity", MessageTemplate.Format("{v}", MessageArguments.Of(("v", double.PositiveInfinity)), CultureInfo.GetCultureInfo("en-US")));
        Assert.Equal("NaN", MessageTemplate.Format("{v:N2}", MessageArguments.Of(("v", double.NaN)), CultureInfo.GetCultureInfo("zh-TW")));
    }

    [Fact]
    public void Format_MapsNativeValuesIntoTheValueModel()
    {
        // 各宽度的整数在 64 位范围内是整数，超出时是浮点；单精度与十进制小数先换成双精度
        Assert.Equal("9223372036854775807", MessageTemplate.Format("{v:D}", MessageArguments.Of(("v", (ulong)long.MaxValue)), null));
        Assert.Equal("1.8446744073709552E+19", MessageTemplate.Format("{v}", MessageArguments.Of(("v", ulong.MaxValue)), null));
        Assert.Equal("0.10000000149011612", MessageTemplate.Format("{v}", MessageArguments.Of(("v", 0.1f)), null));
        Assert.Equal("2.5", MessageTemplate.Format("{v}", MessageArguments.Of(("v", 2.50m)), null));
        Assert.Equal("c", MessageTemplate.Format("{v}", MessageArguments.Of(("v", 'c')), null));
        Assert.Equal("[1,2]", MessageTemplate.Format("{v}", MessageArguments.Of(("v", new[] { 1, 2 })), null));
        Assert.Equal("{\"a\":1,\"b\":[\"x\"]}", MessageTemplate.Format("{v}", MessageArguments.Of(("v", new Dictionary<string, object?> { ["b"] = new[] { "x" }, ["a"] = 1 })), null));
    }

    [Fact]
    public void Format_IsLenient_ButTryFormatIsStrict()
    {
        var arguments = MessageArguments.Of(("id", 42));

        // 宽松：缺值的占位符原样保留，让遗漏可见
        Assert.Equal("Order '42' for {customer:upper}.", MessageTemplate.Format("Order '{id}' for {customer:upper}.", arguments, CultureInfo.InvariantCulture));
        Assert.False(MessageTemplate.TryFormat("Order '{id}' for {customer}.", arguments, null, out _));
        Assert.False(MessageTemplate.TryFormat("Order '{id}'.", null, null, out _));
        Assert.True(MessageTemplate.TryFormat("Order '{id}'.", arguments, null, out var text));
        Assert.Equal("Order '42'.", text);
    }

    [Fact]
    public void PlaceholdersOf_ListsNamesInOrderWithoutDuplicates()
    {
        Assert.Equal(["id", "name", "0"], MessageTemplate.PlaceholdersOf("{id} {name:upper} {id} {{x}} {0}"));
        Assert.Empty(MessageTemplate.PlaceholdersOf("plain {not a name} {{escaped}}"));
    }

    [Fact]
    public void Describe_ShowsKeyAndArguments()
    {
        Assert.Equal("orders.order.not_found", MessageTemplate.Describe("orders.order.not_found"));
        Assert.Equal("orders.order.not_found (entity=Order, id=42)", MessageTemplate.Describe("orders.order.not_found", MessageArguments.Of(("entity", "Order"), ("id", 42))));
    }

    [Fact]
    public void MessageArguments_RejectBlankAndDuplicateNames()
    {
        Assert.Throws<ArgumentException>(static () => MessageArguments.Of(("id", 1), ("id", 2)));
        Assert.Throws<ArgumentException>(static () => MessageArguments.Of((" ", 1)));
        Assert.Equal(["0", "1"], MessageArguments.Positional("a", null).Keys);
        Assert.Empty(MessageArguments.Of());
    }
}
