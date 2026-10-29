// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Protocols.Http.Tests.Fixtures;

/// <summary>
/// 登记测试操作的特性。
/// </summary>
/// <remarks>
/// 五个标准动词、一个实例级与一个集合级自定义动词暴露给 HTTP，一个只给 MCP，一个标为内部。
/// </remarks>
internal sealed class OrdersFeature : IFeature
{
    /// <summary>
    /// 暴露给 HTTP 且非内部的操作 ID。
    /// </summary>
    public static readonly string[] HttpOperations = ["order-filters.delete", "order-filters.list", "order-searches.list", "orders.cancel", "orders.create", "orders.delete", "orders.export", "orders.get", "orders.list", "orders.report", "orders.update"];

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.test.orders",
        Package = "XiHan.Framework.Protocols.Http.Tests",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Entry = typeof(OrdersFeature).FullName,
    };

    /// <summary>
    /// 手写的请求 Schema：带 $defs 的嵌套定义，验证 OpenAPI 把它提升到 components。
    /// </summary>
    public static readonly OperationSchema OrderSchema = new(
        """{"$schema":"https://json-schema.org/draft/2020-12/schema","title":"OrderRequest","type":"object","properties":{"id":{"type":["string","null"]},"name":{"type":["string","null"]},"page":{"type":"integer","format":"int32"},"tags":{"type":["array","null"],"items":{"type":"string"}},"line":{"$ref":"#/$defs/Line"}},"required":["page"],"$defs":{"Line":{"type":"object","properties":{"sku":{"type":"string"}},"required":["sku"]}}}""",
        """{"$schema":"https://json-schema.org/draft/2020-12/schema","title":"OrderReply","type":"object","properties":{"operation":{"type":"string"}},"required":["operation"]}""");

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        foreach (var id in HttpOperations.Where(static id => id.StartsWith("orders.", StringComparison.Ordinal) && id != "orders.report"))
        {
            // get 与 create 带 Schema，其余不带：OpenAPI 两种情况都要能发布
            Add(context, new OperationContract(id, expose: Expose.Http), id is "orders.get" or "orders.create" ? OrderSchema : null);
        }

        // 请求不含 id 的自定义动词：按约定走集合级路由 POST /orders:report
        context.AddOperation<OrderReportOperation, OrderReportRequest, OrderReply>(new OperationContract("orders.report", expose: Expose.Http), OrdersJsonContext.Default.OrderReportRequest, OrdersJsonContext.Default.OrderReply);
        // 类型化绑定：GET /order-filters 与 DELETE /order-filters/{id}，字段只能从路由值与查询串来
        foreach (var id in new[] { "order-filters.list", "order-filters.delete" })
        {
            context.AddOperation<OrderFilterOperation, OrderFilterRequest, OrderFilterRequest>(new OperationContract(id, expose: Expose.Http), OrdersJsonContext.Default.OrderFilterRequest, OrdersJsonContext.Default.OrderFilterRequest);
        }

        // 分页请求带嵌套对象：list 推导为 POST /order-searches:list
        context.AddOperation<OrderSearchOperation, OrderSearchRequest, OrderSearchRequest>(new OperationContract("order-searches.list", expose: Expose.Http), OrdersJsonContext.Default.OrderSearchRequest, OrdersJsonContext.Default.OrderSearchRequest);

        Add(context, new OperationContract("orders.secret", expose: Expose.Mcp));
        Add(context, new OperationContract("orders.audit", Stability.Internal, Expose.All));
    }

    /// <summary>
    /// 以同一实现登记一个契约。
    /// </summary>
    private static void Add(FeatureContext context, OperationContract contract, OperationSchema? schema = null)
        => context.AddOperation<OrdersOperation, OrderRequest, OrderReply>(contract, OrdersJsonContext.Default.OrderRequest, OrdersJsonContext.Default.OrderReply, schema);
}
