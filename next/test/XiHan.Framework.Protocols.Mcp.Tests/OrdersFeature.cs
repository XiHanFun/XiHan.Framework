// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Protocols.Mcp.Tests;

/// <summary>
/// 登记八个操作 ID 的测试特性。
/// </summary>
/// <remarks>
/// 六个暴露给 MCP（<c>orders.create</c> 带 <c>orders.write</c> 权限，<c>create</c> 与 <c>get</c> 带 Schema），一个只给 HTTP，一个标为内部。
/// </remarks>
internal sealed class OrdersFeature : IFeature
{
    /// <summary>
    /// 暴露给 MCP 且非内部的操作 ID。
    /// </summary>
    public static readonly string[] McpOperations = ["order-notes.list", "orders.cancel", "orders.create", "orders.delete", "orders.get", "orders.list", "orders.update"];

    /// <summary>
    /// 手写的 Schema：请求带 $defs 的嵌套定义，响应为对象。
    /// </summary>
    public static readonly OperationSchema OrderSchema = new(
        """{"$schema":"https://json-schema.org/draft/2020-12/schema","title":"OrderRequest","type":"object","properties":{"id":{"type":["string","null"]},"name":{"type":["string","null"]},"page":{"type":"integer","format":"int32"},"tags":{"type":["array","null"],"items":{"type":"string"}},"line":{"$ref":"#/$defs/Line"}},"required":["page"],"$defs":{"Line":{"type":"object","properties":{"sku":{"type":"string"}},"required":["sku"]}}}""",
        """{"$schema":"https://json-schema.org/draft/2020-12/schema","title":"OrderReply","type":"object","properties":{"operation":{"type":"string"}},"required":["operation"]}""");

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.test.orders",
        Package = "XiHan.Framework.Protocols.Mcp.Tests",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        // 要求授权能力：安全特性据此装配 Authenticate / Authorize 阶段，按契约的权限判定 tools/call
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version), new CapabilityRequirement("security.authorization", 1)],
        Entry = typeof(OrdersFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        context.AddPermission(new PermissionDefinition("orders.read", "Read orders"));
        context.AddPermission(new PermissionDefinition("orders.write", "Place orders"));
        foreach (var id in McpOperations)
        {
            var contract = id == "orders.create"
                ? new OperationContract(id, expose: Expose.Mcp | Expose.Http, permission: "orders.write")
                : new OperationContract(id, expose: Expose.Mcp);
            Add(context, contract, id is "orders.get" or "orders.create" ? OrderSchema : null);
        }

        Add(context, new OperationContract("orders.secret", expose: Expose.Http));
        Add(context, new OperationContract("orders.audit", Stability.Internal, Expose.All));
    }

    /// <summary>
    /// 以同一实现登记一个契约。
    /// </summary>
    private static void Add(FeatureContext context, OperationContract contract, OperationSchema? schema = null)
        => context.AddOperation<OrdersOperation, OrderRequest, OrderReply>(contract, OrdersJsonContext.Default.OrderRequest, OrdersJsonContext.Default.OrderReply, schema);
}
