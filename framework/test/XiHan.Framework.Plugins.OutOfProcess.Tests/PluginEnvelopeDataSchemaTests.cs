// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using System.Text.Json;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Plugins.Protocol.Mapping;
using MessageEnvelope = XiHan.Framework.Messaging.Abstractions.Envelopes.MessageEnvelope;
using ProtoEnvelope = XiHan.Framework.Plugins.Protocol.V1.Envelope;
using ProtoStatus = XiHan.Framework.Plugins.Protocol.V1.Status;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 插件协议信封的 <c>data_schema</c>：宿主与插件之间双向原样传递
/// </summary>
/// <remarks>
/// <c>spec/plugin/invocation.md</c> §4，ADR-0305：空串表示没有，其余照原样；只有空白的与 <c>source</c> 只有空白时一样拒绝那一项。Wasm 载体的 proto3 JSON 字段名是 <c>dataSchema</c>
/// </remarks>
public sealed class PluginEnvelopeDataSchemaTests
{
    [Fact]
    public void ToProto_WritesTheDataSchema_AndAnEmptyStringWhenThereIsNone()
    {
        var envelope = new MessageEnvelope { Id = "e1", Source = "/orders", Type = "orders.placed", DataSchema = "https://example.com/schemas/order.json" };

        Assert.Equal("https://example.com/schemas/order.json", PluginEnvelopeMapper.ToProto("orders", envelope).DataSchema);
        Assert.Equal(string.Empty, PluginEnvelopeMapper.ToProto("orders", envelope with { DataSchema = null }).DataSchema);
    }

    [Theory]
    [InlineData("https://example.com/schemas/order.json")]
    [InlineData("urn:example:schemas:order:1")]
    [InlineData("schemas/order.json")]
    [InlineData(" https://example.com/schemas/order.json ")]
    public void FromProto_ReadsTheDataSchema_AsWritten(string schema)
    {
        // 不裁剪、不规范化、不核对 URI 语法：与 source 同一条规则
        var read = PluginEnvelopeMapper.FromProto(new ProtoEnvelope { Id = "e1", Source = "/plugins", Type = "t", DataSchema = schema });

        Assert.Equal(schema, read.ValueOrThrow().DataSchema);
        Assert.Empty(read.ValueOrThrow().Extensions);
    }

    [Fact]
    public void FromProto_ReadsAnEmptyDataSchema_AsNone()
    {
        // proto3 的字符串分不出空串与没有：按较早的规范实现的插件不写这个字段，读到的也是空串
        Assert.Null(PluginEnvelopeMapper.FromProto(new ProtoEnvelope { Id = "e1", Source = "/plugins", Type = "t" }).ValueOrThrow().DataSchema);
    }

    [Fact]
    public void FromProto_RejectsABlankDataSchema_TheWayItRejectsABlankSource()
    {
        var schema = PluginEnvelopeMapper.FromProto(new ProtoEnvelope { Id = "e1", Source = "/plugins", Type = "t", DataSchema = " " }).Error;
        var source = PluginEnvelopeMapper.FromProto(new ProtoEnvelope { Id = "e1", Source = " ", Type = "t" }).Error;

        Assert.Equal((MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeInvalid), (schema?.Code, schema?.MessageKey));
        Assert.Equal((source?.Code, source?.MessageKey), (schema?.Code, schema?.MessageKey));
    }

    [Fact]
    public void APublish_CarriesTheDataSchema_AndABlankOneFailsOnlyItsItem()
    {
        // 逐项的核对里「信封能否转换」那一步（invocation.md §5）：只有空白的那一项不发布，其余照常，数据模式随信封进发布批次
        ProtoEnvelope[] envelopes =
        [
            new() { Topic = "orders", Id = "e1", Source = "/plugins", Type = "t", DataSchema = "https://example.com/schemas/order.json" },
            new() { Topic = "orders", Id = "e2", Source = "/plugins", Type = "t", DataSchema = "\t" },
            new() { Topic = "orders", Id = "e3", Source = "/plugins", Type = "t" },
        ];
        var statuses = new ProtoStatus[envelopes.Length];

        var batch = Assert.Single(PluginPublishBatches.Collect(envelopes, statuses, null, static _ => null));

        Assert.Equal([0, 2], batch.Indexes);
        Assert.Equal(["https://example.com/schemas/order.json", null], batch.Messages.Select(static m => m.DataSchema));
        Assert.Equal(("MESSAGING_VALIDATION_002", MessagingMessages.EnvelopeInvalid), (statuses[1].Error.Code, statuses[1].Error.MessageKey));
    }

    [Fact]
    public void TheWasmCarrier_NamesTheFieldDataSchema_AndIgnoresItWhenAbsent()
    {
        // Wasm 载体是 proto3 JSON（wasm.md §2）：字段名 lowerCamelCase，缺省值省略
        var proto = PluginEnvelopeMapper.ToProto("orders", new MessageEnvelope { Id = "e1", Source = "/orders", Type = "t", DataSchema = "https://example.com/schemas/order.json" });
        using (var json = JsonDocument.Parse(PluginJsonCodec.Encode(proto)))
        {
            Assert.Equal("https://example.com/schemas/order.json", json.RootElement.GetProperty("dataSchema").GetString());
        }

        using (var none = JsonDocument.Parse(PluginJsonCodec.Encode(PluginEnvelopeMapper.ToProto("orders", new MessageEnvelope { Id = "e1", Source = "/orders", Type = "t" }))))
        {
            Assert.False(none.RootElement.TryGetProperty("dataSchema", out _));
        }

        var read = PluginJsonCodec.Decode<ProtoEnvelope>(Encoding.UTF8.GetBytes("""{"topic":"orders","id":"e1","source":"/plugins","type":"t","dataSchema":"urn:example:schemas:order:1"}"""));
        Assert.NotNull(read);
        Assert.Equal("urn:example:schemas:order:1", PluginEnvelopeMapper.FromProto(read).ValueOrThrow().DataSchema);
    }
}
