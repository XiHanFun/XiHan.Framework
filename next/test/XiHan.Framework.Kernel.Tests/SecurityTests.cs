// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests;

public sealed class SecurityTests
{
    private static readonly ServiceProvider Services = new ServiceCollection().BuildServiceProvider();

    [Fact]
    public void ResourceName_ParsesAndFormats()
    {
        var name = ResourceName.Parse("xrn:cn:orders:t-42:order/1001");

        Assert.Equal("cn", name.Partition);
        Assert.Equal("orders", name.Service);
        Assert.Equal("t-42", name.Tenant);
        Assert.Equal("order", name.Type);
        Assert.Equal("1001", name.Id);
        Assert.Equal("xrn:cn:orders:t-42:order/1001", name.ToString());
        Assert.True(ResourceName.TryParse("xrn:cn:orders::order/global", out var global));
        Assert.Equal(string.Empty, global.Tenant);
        Assert.False(ResourceName.TryParse("arn:cn:orders:t:order/1", out _));
        Assert.Throws<ArgumentException>(() => ResourceName.Parse("xrn:cn:orders"));
    }

    [Fact]
    public void Principal_ClaimsHelpers()
    {
        var principal = new Principal("u-1", "jwt") { Claims = [new("role", "admin"), new("role", "ops")] };

        Assert.True(principal.IsAuthenticated);
        Assert.False(Principal.Anonymous.IsAuthenticated);
        Assert.Equal(["admin", "ops"], principal.ClaimValues("role"));
        Assert.True(principal.HasClaim("role", "ops"));
        Assert.False(principal.HasClaim("role", "root"));
    }

    [Fact]
    public void SecurityContext_DefaultsToAnonymousAndCanBeReplaced()
    {
        var context = App.CreateContext(Services, CancellationToken.None);

        Assert.Same(SecurityContext.Anonymous, context.GetSecurity());

        context.SetSecurity(new SecurityContext(new Principal("u-1", "jwt"), null));
        context.SetTenant(new TenantContext("t-42"));

        Assert.Equal("u-1", context.GetSecurity().Principal.Id);
        Assert.Equal("t-42", context.GetSecurity().Tenant!.Id);
        Assert.Throws<ArgumentException>(() => new TenantContext(" "));
    }

    [Fact]
    public void PermissionRegistry_IsBoundedAndRejectsDuplicates()
    {
        var registry = new PermissionRegistry(capacity: 2);

        Assert.True(registry.Add(new PermissionDefinition("orders.order.read", "读取订单")).IsSuccess);
        Assert.Equal(KernelErrors.DuplicatePermission, registry.Add(new PermissionDefinition("orders.order.read", "重复")).Error!.Code);
        Assert.True(registry.Add(new PermissionDefinition("orders.order.write", "写入订单")).IsSuccess);
        Assert.Equal(KernelErrors.PermissionRegistryFull, registry.Add(new PermissionDefinition("orders.order.delete", "删除")).Error!.Code);
        Assert.Equal(2, registry.Count);
        Assert.Equal("读取订单", registry.Find("orders.order.read").ValueOrThrow().DisplayName);
        Assert.False(registry.Find("nope.nope").HasValue);
        Assert.Equal(["orders.order.read", "orders.order.write"], registry.All().Select(static p => p.Code));
        Assert.Throws<ArgumentException>(() => new PermissionDefinition("Orders.Read", "大写"));
        Assert.Throws<ArgumentException>(() => new PermissionDefinition("orders", "单段"));
    }

    [Fact]
    public async Task AuthorizationResult_ConvertsToResult()
    {
        Assert.True(AuthorizationResult.Allow().ToResult().IsSuccess);
        Assert.Equal("SECURITY_DENIED_002", AuthorizationResult.Deny(Error.Denied("SECURITY_DENIED_002", "拒绝")).ToResult().Error!.Code);
        Assert.False(default(AuthorizationResult).IsAllowed);

        IPolicy policy = new DenyAll();
        var result = await policy.EvaluateAsync(new AuthorizationRequest(SecurityContext.Anonymous, "orders.create"), CancellationToken.None);
        Assert.False(result.IsAllowed);
        Assert.Throws<ArgumentException>(() => new AuthorizationRequest(SecurityContext.Anonymous, " "));
    }

    private sealed class DenyAll : IPolicy
    {
        public string Name => "deny-all";

        public ValueTask<AuthorizationResult> EvaluateAsync(AuthorizationRequest request, CancellationToken cancellationToken)
            => ValueTask.FromResult(AuthorizationResult.Deny(Error.Denied("SECURITY_DENIED_002", "deny-all")));
    }
}
