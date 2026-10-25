// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions.Authorization;
using XiHan.Framework.Security.Abstractions.Permissions;
using XiHan.Framework.Security.Abstractions.Tests.Fixtures;

namespace XiHan.Framework.Security.Abstractions.Tests.Authorization;

/// <summary>
/// Authorize 阶段按操作契约推导授权要求：权限码、策略名、无要求放行、显式要求优先。
/// </summary>
public sealed class OperationContractAuthorizationTests
{
    /// <summary>
    /// 编辑角色可读订单。
    /// </summary>
    private static readonly PermissionGrant[] _grants = [new(GrantSubject.ForRole("editor"), "orders.read")];

    /// <summary>
    /// 带已认证策略、订单读权限定义与授予的容器。
    /// </summary>
    private static IServiceProvider Services()
        => SecurityHosts.Services(s => s.AddSingleton(new PolicyCatalog(
            [new AuthenticatedPolicy()],
            SecurityHosts.Registry(SecurityHosts.OrdersRead),
            new PermissionChecker(new DictionaryGrantStore(_grants), new SecurityOptions()))));

    /// <summary>
    /// 带指定契约的操作描述。
    /// </summary>
    private static OperationDescriptor<NoopOperation, NoopRequest, NoopResponse> Descriptor(OperationContract contract)
        => new(contract, "xihan.test.security", NoopJsonContext.Default.NoopRequest, NoopJsonContext.Default.NoopResponse);

    /// <summary>
    /// 带操作描述与可选主体的上下文。
    /// </summary>
    private static InvocationContext Context(OperationContract contract, Principal? principal = null)
    {
        var context = SecurityHosts.Context(Services());
        context.SetOperation(Descriptor(contract));
        if (principal is not null)
        {
            context.SetSecurity(new SecurityContext(principal, null));
        }

        return context;
    }

    /// <summary>
    /// 跑授权中间件并返回终点是否到达。
    /// </summary>
    private static async Task<bool> RunAsync(InvocationContext context)
    {
        var reached = false;
        await new AuthorizationMiddleware().InvokeAsync(context, _ =>
        {
            reached = true;
            return ValueTask.CompletedTask;
        });
        return reached;
    }

    [Fact]
    public async Task PermissionContract_RequiresTheGrant()
    {
        var contract = new OperationContract("orders.list", permission: "orders.read");

        Assert.True(await RunAsync(Context(contract, SecurityHosts.Alice("editor"))));

        var viewer = Context(contract, SecurityHosts.Alice("viewer"));
        Assert.False(await RunAsync(viewer));
        Assert.Equal(SecurityErrors.PermissionMissing, viewer.GetOutcome().ValueOrThrow().Error?.Code);

        var anonymous = Context(contract);
        Assert.False(await RunAsync(anonymous));
        Assert.Equal(SecurityErrors.NotAuthenticated, anonymous.GetOutcome().ValueOrThrow().Error?.Code);

        // 未定义的权限码按策略不存在拒绝，配置错误不能变成越权
        var undefined = Context(new OperationContract("orders.delete", permission: "orders.delete"), SecurityHosts.Alice("editor"));
        Assert.False(await RunAsync(undefined));
        Assert.Equal(SecurityErrors.PolicyNotFound, undefined.GetOutcome().ValueOrThrow().Error?.Code);
    }

    [Fact]
    public async Task PolicyContract_AndNoContract()
    {
        var authenticated = new OperationContract("orders.list", policy: AuthenticatedPolicy.PolicyName);
        Assert.True(await RunAsync(Context(authenticated, SecurityHosts.Alice())));
        var anonymous = Context(authenticated);
        Assert.False(await RunAsync(anonymous));
        Assert.Equal(SecurityErrors.NotAuthenticated, anonymous.GetOutcome().ValueOrThrow().Error?.Code);

        // 没有授权要求的契约放行匿名调用
        Assert.True(await RunAsync(Context(new OperationContract("orders.list"))));
    }

    [Fact]
    public async Task ExplicitRequirement_TakesPrecedenceOverContract()
    {
        var context = Context(new OperationContract("orders.list", permission: "orders.read"), SecurityHosts.Alice("editor"));
        context.RequireAuthorization(new AuthorizationRequirement("missing"));

        Assert.False(await RunAsync(context));
        Assert.Equal(SecurityErrors.PolicyNotFound, context.GetOutcome().ValueOrThrow().Error?.Code);
    }
}
