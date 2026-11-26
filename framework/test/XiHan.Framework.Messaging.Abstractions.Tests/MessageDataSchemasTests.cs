// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Messaging.Abstractions.Envelopes;

namespace XiHan.Framework.Messaging.Abstractions.Tests;

/// <summary>
/// 按 CloudEvents <c>type</c> 登记的数据模式
/// </summary>
/// <remarks>
/// RFC-0009「框架事件的数据模式」（ADR-0323）：取值照「信封」的 <c>dataschema</c>，登记时就拒，同值只记一次、异值是装配错误，登记表有界
/// </remarks>
public sealed class MessageDataSchemasTests
{
    /// <summary>
    /// 孤立的高代理项
    /// </summary>
    private const char HighSurrogate = (char)0xD800;

    [Fact]
    public void AddMessageDataSchema_SharesOneRegistry_AndFindsByTheOrdinalType()
    {
        var services = new ServiceCollection();

        services.AddMessageDataSchema("orders.placed", "https://example.com/schemas/order-placed.json")
            .AddMessageDataSchema("orders.shipped", "urn:example:order-shipped")
            .AddMessageDataSchema("orders.placed", "https://example.com/schemas/order-placed.json");

        var schemas = Assert.Single(services, static d => d.ServiceType == typeof(MessageDataSchemas)).ImplementationInstance as MessageDataSchemas;
        Assert.NotNull(schemas);
        Assert.Equal(2, schemas.Count);
        Assert.Equal("https://example.com/schemas/order-placed.json", schemas.Find("orders.placed").ValueOrThrow());
        // 与 MessageEnvelope.DataSchema 一样不核对 URI 语法：URN 与相对引用都照收
        Assert.Equal("urn:example:order-shipped", schemas.Find("orders.shipped").ValueOrThrow());
        Assert.False(schemas.Find("Orders.Placed").HasValue);
        Assert.False(schemas.Find("orders.cancelled").HasValue);
        Assert.Same(schemas, services.BuildServiceProvider().GetRequiredService<MessageDataSchemas>());
    }

    [Fact]
    public void AddMessageDataSchema_RejectsAnotherValueForTheSameType()
    {
        var services = new ServiceCollection().AddMessageDataSchema("orders.placed", "urn:a");

        // 不取先登记的也不取后登记的：两处对同一个 type 有分歧是装配错误
        var conflict = Assert.Throws<ArgumentException>(() => services.AddMessageDataSchema("orders.placed", "urn:b"));

        Assert.Contains("'orders.placed'", conflict.Message, StringComparison.Ordinal);
        Assert.Equal("urn:a", services.GetMessageDataSchemas().Find("orders.placed").ValueOrThrow());
    }

    [Fact]
    public void AddMessageDataSchema_RejectsValuesAnEnvelopeCouldNotCarryOrWrite()
    {
        var services = new ServiceCollection();

        // 与 MessageEnvelope.Type、MessageEnvelope.DataSchema 的 init 同一条规则；孤立的代理项写不进结构化 JSON 与 binary 的头
        Assert.Throws<ArgumentNullException>(() => services.AddMessageDataSchema("orders.placed", null!));
        Assert.Throws<ArgumentException>(() => services.AddMessageDataSchema("orders.placed", string.Empty));
        Assert.Throws<ArgumentException>(() => services.AddMessageDataSchema("orders.placed", " "));
        Assert.Throws<ArgumentException>(() => services.AddMessageDataSchema("orders.placed", "urn:" + HighSurrogate));
        Assert.Throws<ArgumentNullException>(() => services.AddMessageDataSchema(null!, "urn:a"));
        Assert.Throws<ArgumentException>(() => services.AddMessageDataSchema(" ", "urn:a"));
        Assert.Throws<ArgumentException>(() => services.AddMessageDataSchema("orders" + HighSurrogate, "urn:a"));
        Assert.Equal(0, services.GetMessageDataSchemas().Count);

        // 成对的代理项是合法的文本
        services.AddMessageDataSchema("orders.😀", "urn:😀");
        Assert.Equal("urn:😀", services.GetMessageDataSchemas().Find("orders.😀").ValueOrThrow());
    }

    [Fact]
    public void Registry_IsBounded_ButRepeatingARegisteredTypeStillWorks()
    {
        var schemas = new MessageDataSchemas(capacity: 1);
        schemas.Add("orders.placed", "urn:a");

        Assert.Throws<InvalidOperationException>(() => schemas.Add("orders.shipped", "urn:b"));
        schemas.Add("orders.placed", "urn:a");
        Assert.Equal(1, schemas.Count);
        Assert.Throws<ArgumentOutOfRangeException>(static () => new MessageDataSchemas(0));
    }
}
