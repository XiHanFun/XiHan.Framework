// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers;
using System.Text;
using System.Text.Json;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;
using XiHan.Framework.Protocols.Grpc.Methods;
using XiHan.Framework.Protocols.Grpc.Proto;
using XiHan.Framework.Protocols.Grpc.Tests.Fixtures;

namespace XiHan.Framework.Protocols.Grpc.Tests.Proto;

/// <summary>
/// JSON Schema → protobuf 描述、编码与解码、<c>.proto</c> 生成。
/// </summary>
public sealed class ProtoCodecTests
{
    /// <summary>
    /// 覆盖全部映射的 Schema。
    /// </summary>
    /// <remarks>
    /// 标量与格式、可空、字节、枚举、数组、嵌套与可空嵌套、映射、任意 JSON、snake_case 的 JSON 名。
    /// </remarks>
    private const string RichSchema = """
        {"$schema":"https://json-schema.org/draft/2020-12/schema","title":"Rich","type":"object","properties":{
          "s":{"type":"string"},"ns":{"type":["string","null"]},"b":{"type":"boolean"},"i":{"type":"integer","format":"int32"},"l":{"type":"integer","format":"int64"},
          "f":{"type":"number","format":"float"},"d":{"type":"number","format":"double"},"m":{"type":"number","format":"decimal"},"big":{"type":"integer"},
          "bytes":{"type":"string","contentEncoding":"base64"},"createdAt":{"type":"string","format":"date-time"},"e":{"type":"string","enum":["A","B"]},
          "ints":{"type":"array","items":{"type":"integer","format":"int32"}},"names":{"type":["array","null"],"items":{"type":"string"}},
          "child":{"$ref":"#/$defs/Child"},"maybe":{"anyOf":[{"$ref":"#/$defs/Child"},{"type":"null"}]},"children":{"type":"array","items":{"$ref":"#/$defs/Child"}},
          "prices":{"type":"object","additionalProperties":{"type":"number","format":"double"}},"byName":{"type":["object","null"],"additionalProperties":{"$ref":"#/$defs/Child"}},
          "any":{},"order_id":{"type":"string"}},
          "required":["s","b","i","l","f","d","m","bytes","createdAt","e","ints","child","children","prices","order_id"],
          "$defs":{"Child":{"type":"object","properties":{"sku":{"type":"string"},"qty":{"type":"integer","format":"int32"}},"required":["sku","qty"]}}}
        """;

    /// <summary>
    /// 一个覆盖全部字段的值。
    /// </summary>
    private const string RichValue = """{"s":"hi","ns":null,"b":true,"i":-5,"l":1234567890123,"f":1.5,"d":2.25,"m":12.50,"big":123456789012345678901234567890,"bytes":"AQID","createdAt":"2026-10-23T00:00:00Z","e":"B","ints":[1,2,300],"names":["x"],"child":{"sku":"a","qty":1},"maybe":null,"children":[{"sku":"b","qty":2}],"prices":{"a":1.5},"byName":{"k":{"sku":"c","qty":3}},"any":{"x":[1,2]},"order_id":"o-1"}""";

    /// <summary>
    /// 编码一个 JSON 文本。
    /// </summary>
    private static byte[] Encode(ProtoMessage message, string json)
    {
        using var document = JsonDocument.Parse(json);
        var output = new ArrayBufferWriter<byte>();
        ProtoEncoder.Encode(message, document.RootElement, output);
        return output.WrittenMemory.ToArray();
    }

    /// <summary>
    /// 解码为 JSON 文本。
    /// </summary>
    private static string Decode(ProtoMessage message, byte[] bytes)
    {
        var output = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(output))
        {
            ProtoDecoder.Decode(message, bytes, writer);
        }

        return Encoding.UTF8.GetString(output.WrittenSpan);
    }

    [Fact]
    public void Schema_MapsEveryShape()
    {
        var message = ProtoSchema.FromJsonSchema(RichSchema);
        var fields = message.Fields.ToDictionary(static f => f.JsonName, static f => f, StringComparer.Ordinal);

        Assert.Equal("Rich", message.Name);
        Assert.Equal(Enumerable.Range(1, 21), message.Fields.Select(static f => f.Number));
        Assert.Equal((ProtoKind.Text, ProtoCardinality.Singular), (fields["s"].Value.Kind, fields["s"].Cardinality));
        Assert.Equal((ProtoKind.Text, ProtoCardinality.Optional), (fields["ns"].Value.Kind, fields["ns"].Cardinality));
        Assert.Equal(ProtoKind.Bool, fields["b"].Value.Kind);
        Assert.Equal(ProtoKind.Varint32, fields["i"].Value.Kind);
        Assert.Equal(ProtoKind.Varint64, fields["l"].Value.Kind);
        Assert.Equal(ProtoKind.Fixed32, fields["f"].Value.Kind);
        Assert.Equal(ProtoKind.Fixed64, fields["d"].Value.Kind);
        Assert.Equal(ProtoKind.TextNumber, fields["m"].Value.Kind);
        Assert.Equal(ProtoKind.TextNumber, fields["big"].Value.Kind);
        Assert.Equal(ProtoKind.Bytes, fields["bytes"].Value.Kind);
        Assert.Equal(ProtoKind.Text, fields["createdAt"].Value.Kind);
        Assert.Equal(ProtoKind.Text, fields["e"].Value.Kind);
        Assert.Equal((ProtoKind.Varint32, ProtoCardinality.Repeated), (fields["ints"].Value.Kind, fields["ints"].Cardinality));
        Assert.Equal((ProtoKind.Text, ProtoCardinality.Repeated), (fields["names"].Value.Kind, fields["names"].Cardinality));
        Assert.Equal((ProtoKind.Message, ProtoCardinality.Singular, "Child"), (fields["child"].Value.Kind, fields["child"].Cardinality, fields["child"].Value.Message!.Name));
        Assert.Equal(ProtoCardinality.Optional, fields["maybe"].Cardinality);
        Assert.Equal((ProtoKind.Message, ProtoCardinality.Repeated), (fields["children"].Value.Kind, fields["children"].Cardinality));
        Assert.Equal((ProtoKind.Fixed64, ProtoCardinality.Map), (fields["prices"].Value.Kind, fields["prices"].Cardinality));
        Assert.Equal((ProtoKind.Message, ProtoCardinality.Map), (fields["byName"].Value.Kind, fields["byName"].Cardinality));
        Assert.Equal(ProtoKind.Json, fields["any"].Value.Kind);
        Assert.Same(fields["child"].Value.Message, fields["children"].Value.Message);

        // 不可表示的形态：嵌套数组、根不是对象、引用不存在
        Assert.Throws<ProtoException>(static () => ProtoSchema.FromJsonSchema("""{"title":"X","type":"object","properties":{"m":{"type":"array","items":{"type":"array","items":{"type":"string"}}}}}"""));
        Assert.Throws<ProtoException>(static () => ProtoSchema.FromJsonSchema("""{"title":"X","type":"array","items":{"type":"string"}}"""));
        Assert.Throws<ProtoException>(static () => ProtoSchema.FromJsonSchema("""{"title":"X","type":"object","properties":{"c":{"$ref":"#/$defs/Nope"}}}"""));
        Assert.Throws<ProtoException>(static () => ProtoSchema.FromJsonSchema("not json"));
    }

    [Fact]
    public void Codec_RoundTrips_AndMatchesWireFormat()
    {
        var message = ProtoSchema.FromJsonSchema(RichSchema);
        var bytes = Encode(message, RichValue);

        // 与手工拼出的线格式逐字段比对：负的 int32 是 10 字节 varint，int32 数组打包，映射条目是嵌套消息
        Assert.True(bytes.AsSpan().StartsWith(ProtoBytes.Concat(ProtoBytes.StringField(1, "hi"), ProtoBytes.VarintField(3, 1), ProtoBytes.VarintField(4, -5), ProtoBytes.VarintField(5, 1234567890123))));
        Assert.Contains(ProtoBytes.BytesField(13, ProtoBytes.Concat(ProtoBytes.Varint(1), ProtoBytes.Varint(2), ProtoBytes.Varint(300))), bytes);
        Assert.Contains(ProtoBytes.BytesField(18, ProtoBytes.Concat(ProtoBytes.StringField(1, "a"), [0x11, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF8, 0x3F])), bytes);
        Assert.Contains(ProtoBytes.StringField(8, "12.50"), bytes);
        Assert.Contains(ProtoBytes.StringField(9, "123456789012345678901234567890"), bytes);
        Assert.Contains(ProtoBytes.BytesField(10, [1, 2, 3]), bytes);
        Assert.Contains(ProtoBytes.BytesField(15, ProtoBytes.Concat(ProtoBytes.StringField(1, "a"), ProtoBytes.VarintField(2, 1))), bytes);
        Assert.Contains(ProtoBytes.StringField(20, """{"x":[1,2]}"""), bytes);

        // 解码回 JSON：null 的字段不出现，其余值一致，属性按字段号排列
        var decoded = Decode(message, bytes);
        Assert.Equal("""{"s":"hi","b":true,"i":-5,"l":1234567890123,"f":1.5,"d":2.25,"m":12.50,"big":123456789012345678901234567890,"bytes":"AQID","createdAt":"2026-10-23T00:00:00Z","e":"B","ints":[1,2,300],"names":["x"],"child":{"sku":"a","qty":1},"children":[{"sku":"b","qty":2}],"prices":{"a":1.5},"byName":{"k":{"sku":"c","qty":3}},"any":{"x":[1,2]},"order_id":"o-1"}""", decoded);

        // 值与 Schema 不符
        Assert.Throws<ProtoException>(() => Encode(message, """{"i":"5"}"""));
        Assert.Throws<ProtoException>(() => Encode(message, """{"ints":{"a":1}}"""));
        Assert.Throws<ProtoException>(() => Encode(message, """{"bytes":"not base64!"}"""));
        Assert.Throws<ProtoException>(() => Encode(message, """[1]"""));
    }

    [Fact]
    public void Decoder_AcceptsPackedAndUnpacked_SkipsUnknown_TakesLast_RejectsMalformed()
    {
        var message = ProtoSchema.FromJsonSchema(RichSchema);

        // 同一 repeated 字段混合打包与不打包；未知字段号 99 跳过；单值字段重复出现取最后一个；缺席的布尔 / 数值 / 集合按 proto3 默认值补出，字符串、消息与 optional 不补
        var bytes = ProtoBytes.Concat(
            ProtoBytes.BytesField(13, ProtoBytes.Concat(ProtoBytes.Varint(1), ProtoBytes.Varint(2))),
            ProtoBytes.VarintField(13, 3),
            ProtoBytes.VarintField(99, 7),
            ProtoBytes.StringField(99, "ignored"),
            ProtoBytes.StringField(1, "first"),
            ProtoBytes.StringField(1, "last"));
        Assert.Equal("""{"s":"last","b":false,"i":0,"l":0,"f":0,"d":0,"m":0,"big":0,"ints":[1,2,3],"names":[],"children":[],"prices":{},"byName":{}}""", Decode(message, bytes));

        // 映射缺值按默认值，缺键按空串
        Assert.Equal("""{"b":false,"i":0,"l":0,"f":0,"d":0,"m":0,"big":0,"ints":[],"names":[],"children":[],"prices":{"a":0,"":1.5},"byName":{}}""", Decode(message, ProtoBytes.Concat(
            ProtoBytes.BytesField(18, ProtoBytes.StringField(1, "a")),
            ProtoBytes.BytesField(18, [0x11, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF8, 0x3F]))));

        // 线类型不符、截断、非法 UTF-8、十进制文本不是数字
        Assert.Throws<ProtoException>(() => Decode(message, ProtoBytes.VarintField(1, 5)));
        Assert.Throws<ProtoException>(() => Decode(message, [0x0A, 0x05, 0x61]));
        Assert.Throws<ProtoException>(() => Decode(message, ProtoBytes.BytesField(1, [0xFF, 0xFE])));
        Assert.Throws<ProtoException>(() => Decode(message, ProtoBytes.StringField(8, "abc")));
        Assert.Throws<ProtoException>(() => Decode(message, [0x08]));
    }

    [Fact]
    public void ProtoFile_RendersMessagesServicesAndJsonNames()
    {
        var rich = ProtoSchema.FromJsonSchema(RichSchema);
        var operations = new List<GrpcOperation>
        {
            new(Descriptor(new OperationContract("orders.get")), new GrpcMethodName("Orders", "Get"), OrdersFeature.RequestMessage, OrdersFeature.ReplyMessage),
            new(Descriptor(new OperationContract("orders.create")), new GrpcMethodName("Orders", "Create"), rich, OrdersFeature.ReplyMessage),
        };

        var proto = ProtoFileBuilder.Build(operations, "demo");

        Assert.StartsWith("// Generated by XiHan Framework", proto, StringComparison.Ordinal);
        Assert.Contains("syntax = \"proto3\";\n\npackage demo;\n", proto, StringComparison.Ordinal);
        Assert.Contains("message OrderRequest {\n  optional string id = 1;\n  optional string name = 2;\n  int32 page = 3;\n  repeated string tags = 4;\n  optional string reason = 5;\n}\n", proto, StringComparison.Ordinal);
        Assert.Contains("message Child {\n  string sku = 1;\n  int32 qty = 2;\n}\n", proto, StringComparison.Ordinal);
        Assert.Contains("  string created_at = 11;\n", proto, StringComparison.Ordinal);
        Assert.Contains("  optional Child maybe = 16;\n", proto, StringComparison.Ordinal);
        Assert.Contains("  repeated Child children = 17;\n", proto, StringComparison.Ordinal);
        Assert.Contains("  map<string, double> prices = 18;\n", proto, StringComparison.Ordinal);
        Assert.Contains("  map<string, Child> by_name = 19;\n", proto, StringComparison.Ordinal);
        Assert.Contains("  string any = 20;\n", proto, StringComparison.Ordinal);
        Assert.Contains("  string order_id = 21 [json_name = \"order_id\"];\n", proto, StringComparison.Ordinal);
        Assert.Contains("  string m = 8;\n", proto, StringComparison.Ordinal);
        Assert.Contains("service Orders {\n  // orders.create\n  rpc Create (Rich) returns (OrderReply);\n  // orders.get\n  rpc Get (OrderRequest) returns (OrderReply);\n}\n", proto, StringComparison.Ordinal);

        // 同名消息形状不同即冲突
        var other = ProtoSchema.FromJsonSchema("""{"title":"OrderReply","type":"object","properties":{"different":{"type":"string"}}}""");
        var conflicting = new List<GrpcOperation> { operations[0], new(Descriptor(new OperationContract("orders.list")), new GrpcMethodName("Orders", "List"), OrdersFeature.RequestMessage, other) };
        Assert.Equal(GrpcErrors.MessageConflict, Assert.Throws<FrameworkException>(() => ProtoFileBuilder.Build(conflicting, null)).Code);
    }

    [Fact]
    public void StringEnumDefinitions_MapToStrings_AndGenericTitlesKeepMessagesApart()
    {
        var users = ProtoSchema.FromJsonSchema(PagedSchemas.UsersPage);
        var roles = ProtoSchema.FromJsonSchema(PagedSchemas.RolesPage);
        var query = ProtoSchema.FromJsonSchema(PagedSchemas.PageQuery);

        // 字符串枚举定义不建消息：$ref 到它的字段按 string 映射，可空为 optional，数组与映射的元素同样是 string
        Assert.Equal("PageResultOfUserResponse", users.Name);
        var user = Assert.Single(users.Fields, static f => f.JsonName == "items").Value.Message!;
        Assert.Equal("UserResponse", user.Name);
        Assert.Equal((ProtoKind.Text, ProtoCardinality.Singular), user.Fields[1].Let(static f => (f.Value.Kind, f.Cardinality)));
        Assert.Equal((ProtoKind.Text, ProtoCardinality.Optional), user.Fields[2].Let(static f => (f.Value.Kind, f.Cardinality)));
        Assert.Equal((ProtoKind.Text, ProtoCardinality.Repeated), user.Fields[3].Let(static f => (f.Value.Kind, f.Cardinality)));
        Assert.Equal((ProtoKind.Text, ProtoCardinality.Map), user.Fields[4].Let(static f => (f.Value.Kind, f.Cardinality)));
        Assert.Equal((ProtoKind.Text, ProtoCardinality.Optional), query.Fields[1].Let(static f => (f.Value.Kind, f.Cardinality)));

        // 线格式不变：枚举名按字符串上线，往返一致
        var bytes = Encode(users, """{"items":[{"id":"u-1","status":"Active","previousStatus":"Disabled","history":["Disabled","Active"],"byRole":{"admin":"Active"}}],"total":1}""");
        Assert.Contains(ProtoBytes.StringField(2, "Active"), bytes);
        Assert.Equal("""{"items":[{"id":"u-1","status":"Active","previousStatus":"Disabled","history":["Disabled","Active"],"byRole":{"admin":"Active"}}],"total":1}""", Decode(users, bytes));

        // 同一泛型的两个封闭类型是两个消息，.proto 里都在、不冲突；枚举没有对应的 message
        var operations = new List<GrpcOperation>
        {
            new(Descriptor(new OperationContract("users.list")), new GrpcMethodName("Users", "List"), query, users),
            new(Descriptor(new OperationContract("roles.list")), new GrpcMethodName("Roles", "List"), query, roles),
        };
        var proto = ProtoFileBuilder.Build(operations, null);
        Assert.Contains("message PageResultOfUserResponse {\n  repeated UserResponse items = 1;\n  int64 total = 2;\n}\n", proto, StringComparison.Ordinal);
        Assert.Contains("message PageResultOfRoleResponse {\n  repeated RoleResponse items = 1;\n  int64 total = 2;\n}\n", proto, StringComparison.Ordinal);
        Assert.Contains("message UserResponse {\n  string id = 1;\n  string status = 2;\n  optional string previous_status = 3;\n  repeated string history = 4;\n  map<string, string> by_role = 5;\n}\n", proto, StringComparison.Ordinal);
        Assert.Contains("message PageQuery {\n  int32 page = 1;\n  optional string status = 2;\n}\n", proto, StringComparison.Ordinal);
        Assert.DoesNotContain("UserStatus", proto, StringComparison.Ordinal);
        Assert.Contains("rpc List (PageQuery) returns (PageResultOfUserResponse);", proto, StringComparison.Ordinal);

        // 标量定义之间成环时失败，而不是无限递归
        Assert.Throws<ProtoException>(static () => ProtoSchema.FromJsonSchema("""{"title":"X","type":"object","properties":{"a":{"$ref":"#/$defs/A"}},"$defs":{"A":{"$ref":"#/$defs/B"},"B":{"$ref":"#/$defs/A"}}}"""));
    }

    [Fact]
    public void MethodNames_FollowConvention_AndOptionsValidate()
    {
        Assert.Equal("Orders/Create", GrpcMethodConvention.Derive(new OperationContract("orders.create")).ToString());
        Assert.Equal("OrderLines/List", GrpcMethodConvention.Derive(new OperationContract("order-lines.list")).ToString());
        Assert.Equal("/acme.orders.Orders/Create", new GrpcMethodName("Orders", "Create").PathFor("acme.orders"));
        Assert.Equal("/Orders/Create", GrpcMethodName.Parse("Orders/Create").PathFor(null));
        Assert.Throws<ArgumentException>(static () => GrpcMethodName.Parse("Orders.Create"));
        Assert.Throws<ArgumentException>(static () => new GrpcMethodName("1Orders", "Create"));
        Assert.Throws<ArgumentException>(static () => new GrpcProtocolOptions { Package = "Acme.Orders" });
        Assert.Throws<ArgumentException>(static () => new GrpcProtocolOptions { Methods = new Dictionary<string, GrpcMethodName> { ["Bad"] = new("A", "B") } });

        var options = new GrpcProtocolOptions { Package = "acme", Methods = new Dictionary<string, GrpcMethodName> { ["orders.export"] = new("Reports", "Export") } };
        Assert.Equal("Reports/Export", options.MethodFor(new OperationContract("orders.export")).ToString());
        Assert.Equal("Orders/Get", options.MethodFor(new OperationContract("orders.get")).ToString());
    }

    /// <summary>
    /// 一个只有契约与 Schema 的描述，供 <c>.proto</c> 生成测试。
    /// </summary>
    private static OperationDescriptor<OrdersOperation, OrderRequest, OrderReply> Descriptor(OperationContract contract)
        => new OperationDescriptor<OrdersOperation, OrderRequest, OrderReply>(contract, "xihan.test.orders", OrdersJsonContext.Default.OrderRequest, OrdersJsonContext.Default.OrderReply, OrdersFeature.OrderSchema);
}
