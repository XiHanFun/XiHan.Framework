// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Protocols.Grpc.Proto;

namespace XiHan.Framework.Protocols.Grpc.Tests.Fixtures;

/// <summary>
/// 登记测试操作的特性：<c>orders.create</c>（带 <c>orders.write</c> 权限）、<c>orders.get</c>、<c>orders.list</c>、<c>order-lines.list</c> 暴露给 gRPC，<c>orders.secret</c> 只给 HTTP，<c>orders.audit</c> 标为内部。
/// 请求与响应的 Schema 与 <see cref="OrderRequest"/> / <see cref="OrderReply"/> 一一对应。
/// </summary>
internal sealed class OrdersFeature : IFeature
{
    /// <summary>
    /// 请求 Schema：id = 1，name = 2，page = 3，tags = 4，reason = 5。
    /// </summary>
    public const string RequestSchema = """{"$schema":"https://json-schema.org/draft/2020-12/schema","title":"OrderRequest","type":"object","properties":{"id":{"type":["string","null"]},"name":{"type":["string","null"]},"page":{"type":"integer","format":"int32"},"tags":{"type":["array","null"],"items":{"type":"string"}},"reason":{"type":["string","null"]}},"required":["page"]}""";

    /// <summary>
    /// 响应 Schema：operation = 1，id = 2，name = 3，page = 4，tags = 5，reason = 6。
    /// </summary>
    public const string ReplySchema = """{"$schema":"https://json-schema.org/draft/2020-12/schema","title":"OrderReply","type":"object","properties":{"operation":{"type":"string"},"id":{"type":["string","null"]},"name":{"type":["string","null"]},"page":{"type":"integer","format":"int32"},"tags":{"type":"array","items":{"type":"string"}},"reason":{"type":["string","null"]}},"required":["operation","page","tags"]}""";

    /// <summary>
    /// 两个 Schema 组成的操作 Schema。
    /// </summary>
    public static readonly OperationSchema OrderSchema = new(RequestSchema, ReplySchema);

    /// <summary>
    /// 请求的消息描述。
    /// </summary>
    public static readonly ProtoMessage RequestMessage = ProtoSchema.FromJsonSchema(RequestSchema);

    /// <summary>
    /// 响应的消息描述。
    /// </summary>
    public static readonly ProtoMessage ReplyMessage = ProtoSchema.FromJsonSchema(ReplySchema);

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.test.orders",
        Package = "XiHan.Framework.Protocols.Grpc.Tests",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        // 要求授权能力：安全特性据此装配 Authenticate / Authorize 阶段，按契约的权限判定
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version), new CapabilityRequirement("security.authorization", 1)],
        Entry = typeof(OrdersFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        context.AddPermission(new PermissionDefinition("orders.read", "Read orders"));
        context.AddPermission(new PermissionDefinition("orders.write", "Place orders"));
        Add(context, new OperationContract("orders.create", expose: Expose.Grpc | Expose.Http, permission: "orders.write"), OrderSchema);
        Add(context, new OperationContract("orders.get", expose: Expose.Grpc), OrderSchema);
        Add(context, new OperationContract("orders.list", expose: Expose.Grpc), OrderSchema);
        Add(context, new OperationContract("order-lines.list", expose: Expose.Grpc), OrderSchema);
        Add(context, new OperationContract("orders.secret", expose: Expose.Http));
        Add(context, new OperationContract("orders.audit", Stability.Internal, Expose.All));
    }

    /// <summary>
    /// 以同一实现登记一个契约。
    /// </summary>
    private static void Add(FeatureContext context, OperationContract contract, OperationSchema? schema = null)
        => context.AddOperation<OrdersOperation, OrderRequest, OrderReply>(contract, OrdersJsonContext.Default.OrderRequest, OrdersJsonContext.Default.OrderReply, schema);
}
