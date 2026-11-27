// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;

namespace XiHan.Framework.Messaging.Abstractions.Tests;

/// <summary>
/// 从配置节 <c>XiHan:Messaging:DataSchemas</c> 读数据模式
/// </summary>
/// <remarks>
/// RFC-0009「框架事件的数据模式」第 1 条（ADR-0332）：数组形式，每项 <c>Type</c> 与 <c>DataSchema</c>；值的规则、重复与冲突照代码登记，不合即启动失败、消息带配置键。以前只能在代码里登记
/// </remarks>
public sealed class MessageDataSchemaConfigurationTests
{
    /// <summary>
    /// 孤立的高代理项
    /// </summary>
    private const char HighSurrogate = (char)0xD800;

    /// <summary>
    /// 以给定的键值建配置
    /// </summary>
    private static IConfiguration Build(params (string Key, string? Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    [Fact]
    public void FromConfiguration_ReadsEachEntryOfTheArray()
    {
        // type 带点号写不成配置键，所以是数组；环境变量写成 XiHan__Messaging__DataSchemas__0__Type
        var options = MessagingOptions.FromConfiguration(Build(
            ("XiHan:Messaging:DataSchemas:0:Type", "orders.placed"),
            ("XiHan:Messaging:DataSchemas:0:DataSchema", "https://example.com/schemas/order-placed.json"),
            ("XiHan:Messaging:DataSchemas:1:Type", "scheduling.run.failed"),
            ("XiHan:Messaging:DataSchemas:1:DataSchema", "urn:example:run")));

        Assert.Equal(2, options.DataSchemas.Count);
        Assert.Equal("https://example.com/schemas/order-placed.json", options.DataSchemas["orders.placed"]);
        Assert.Equal("urn:example:run", options.DataSchemas["scheduling.run.failed"]);
    }

    [Fact]
    public void FromConfiguration_WithoutTheSection_HasNoDataSchemas_AndEqualsTheDefault()
    {
        var options = MessagingOptions.FromConfiguration(Build());

        Assert.Empty(options.DataSchemas);
        Assert.Equal(new MessagingOptions(), options);
    }

    [Theory]
    [InlineData("Type")]
    [InlineData("DataSchema")]
    public void FromConfiguration_AnEntryMissingAMember_FailsNamingTheKey(string missing)
    {
        var values = new List<(string, string?)> { ("XiHan:Messaging:DataSchemas:0:Type", "orders.placed"), ("XiHan:Messaging:DataSchemas:0:DataSchema", "urn:a") };
        values.RemoveAll(v => v.Item1.EndsWith(":" + missing, StringComparison.Ordinal));

        var thrown = Assert.Throws<ArgumentException>(() => MessagingOptions.FromConfiguration(Build([.. values])));

        Assert.Contains($"'XiHan:Messaging:DataSchemas:0:{missing}'", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromConfiguration_AScalarEntry_FailsNamingTheKey()
    {
        // 写成单个值而不是 Type / DataSchema 两个成员：两个成员都没有
        var thrown = Assert.Throws<ArgumentException>(() => MessagingOptions.FromConfiguration(Build(("XiHan:Messaging:DataSchemas:0", "orders.placed=urn:a"))));

        Assert.Contains("'XiHan:Messaging:DataSchemas:0:Type'", thrown.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Type", "")]
    [InlineData("Type", " ")]
    [InlineData("DataSchema", "")]
    [InlineData("DataSchema", "\t")]
    public void FromConfiguration_AnInvalidValue_FailsNamingTheKey(string member, string value)
    {
        // 与 AddMessageDataSchema 同一条规则：不能为空或只有空白，不能有孤立的代理项
        var type = member == "Type" ? value : "orders.placed";
        var dataSchema = member == "DataSchema" ? value : "urn:a";

        var thrown = Assert.Throws<ArgumentException>(() => MessagingOptions.FromConfiguration(Build(("XiHan:Messaging:DataSchemas:0:Type", type), ("XiHan:Messaging:DataSchemas:0:DataSchema", dataSchema))));

        Assert.Contains($"'XiHan:Messaging:DataSchemas:0:{member}'", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromConfiguration_ALoneSurrogate_FailsNamingTheKey()
    {
        // 孤立的代理项写不进结构化 JSON 与 binary 的头：登记时就拒，免得每次发布都失败
        var type = Assert.Throws<ArgumentException>(() => MessagingOptions.FromConfiguration(Build(("XiHan:Messaging:DataSchemas:0:Type", "orders" + HighSurrogate), ("XiHan:Messaging:DataSchemas:0:DataSchema", "urn:a"))));
        var dataSchema = Assert.Throws<ArgumentException>(() => MessagingOptions.FromConfiguration(Build(("XiHan:Messaging:DataSchemas:0:Type", "orders.placed"), ("XiHan:Messaging:DataSchemas:0:DataSchema", "urn:" + HighSurrogate))));

        Assert.Contains("'XiHan:Messaging:DataSchemas:0:Type'", type.Message, StringComparison.Ordinal);
        Assert.Contains("'XiHan:Messaging:DataSchemas:0:DataSchema'", dataSchema.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromConfiguration_RepeatingAnEntry_RecordsItOnce_ButAnotherValueFails()
    {
        var repeated = MessagingOptions.FromConfiguration(Build(
            ("XiHan:Messaging:DataSchemas:0:Type", "orders.placed"),
            ("XiHan:Messaging:DataSchemas:0:DataSchema", "urn:a"),
            ("XiHan:Messaging:DataSchemas:1:Type", "orders.placed"),
            ("XiHan:Messaging:DataSchemas:1:DataSchema", "urn:a")));
        Assert.Equal("urn:a", Assert.Single(repeated.DataSchemas).Value);

        // 不取先写的也不取后写的：配置里对同一个 type 有分歧是装配错误
        var conflict = Assert.Throws<ArgumentException>(() => MessagingOptions.FromConfiguration(Build(
            ("XiHan:Messaging:DataSchemas:0:Type", "orders.placed"),
            ("XiHan:Messaging:DataSchemas:0:DataSchema", "urn:a"),
            ("XiHan:Messaging:DataSchemas:1:Type", "orders.placed"),
            ("XiHan:Messaging:DataSchemas:1:DataSchema", "urn:b"))));
        Assert.Contains("'XiHan:Messaging:DataSchemas:1:DataSchema'", conflict.Message, StringComparison.Ordinal);
        Assert.Contains("'orders.placed'", conflict.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DataSchemas_Init_ChecksEveryEntry()
    {
        Assert.Throws<ArgumentNullException>(static () => new MessagingOptions { DataSchemas = null! });
        Assert.Throws<ArgumentException>(static () => new MessagingOptions { DataSchemas = new Dictionary<string, string> { [" "] = "urn:a" } });
        Assert.Throws<ArgumentException>(static () => new MessagingOptions { DataSchemas = new Dictionary<string, string> { ["orders.placed"] = string.Empty } });
        Assert.Throws<ArgumentException>(static () => new MessagingOptions { DataSchemas = new Dictionary<string, string> { ["orders.placed"] = "urn:" + HighSurrogate } });
        Assert.Equal("urn:😀", new MessagingOptions { DataSchemas = new Dictionary<string, string> { ["orders.😀"] = "urn:😀" } }.DataSchemas["orders.😀"]);
    }
}
