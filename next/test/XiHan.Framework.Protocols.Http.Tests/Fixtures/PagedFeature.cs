// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Protocols.Http.Tests.Fixtures;

/// <summary>
/// 两个响应为同一泛型、不同实参的列表操作（<c>users.list</c> → <c>PageResult&lt;UserResponse&gt;</c>，<c>roles.list</c> → <c>PageResult&lt;RoleResponse&gt;</c>）暴露给 HTTP，
/// 验证 OpenAPI 把两份根 Schema 与字符串枚举定义都提升为各自的组件。
/// </summary>
internal sealed class PagedFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.test.paged",
        Package = "XiHan.Framework.Protocols.Http.Tests",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Entry = typeof(PagedFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        Add(context, "users.list", PagedSchemas.UsersPage);
        Add(context, "roles.list", PagedSchemas.RolesPage);
    }

    /// <summary>
    /// 以订单实现登记一个只有 Schema 不同的契约。
    /// </summary>
    private static void Add(FeatureContext context, string id, string responseSchema)
        => context.AddOperation<OrdersOperation, OrderRequest, OrderReply>(new OperationContract(id, expose: Expose.Http), OrdersJsonContext.Default.OrderRequest, OrdersJsonContext.Default.OrderReply, new OperationSchema(PagedSchemas.PageQuery, responseSchema));
}
