// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Security;

/// <summary>
/// 安全原语的行为。
/// </summary>
/// <remarks>
/// 资源名解析、主体声明查询、上下文中的安全上下文读写、有界权限注册表与授权结果转换。
/// </remarks>
public sealed class SecurityTests
{
    /// <summary>
    /// 空容器。
    /// </summary>
    /// <remarks>
    /// 这些测试只需要一个服务提供器来创建上下文。
    /// </remarks>
    private static readonly ServiceProvider _services = new ServiceCollection().BuildServiceProvider();

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

    [Theory]
    [InlineData("Acme")]
    [InlineData("acme.cn")]
    [InlineData("t_42")]
    [InlineData("A-01.x_y")]
    [InlineData("0")]
    public void ResourceName_TenantSegment_AcceptsEveryTenantIdShape(string tenant)
    {
        // 租户段与 RFC-0007 的租户 ID 同一文法：大写、点、下划线都能原样往返
        var text = new ResourceName("cn", "shop", tenant, "order", "42").ToString();

        Assert.True(ResourceName.TryParse(text, out var parsed));
        Assert.Equal(tenant, parsed.Tenant);
        Assert.Equal("order", parsed.Type);
        Assert.Equal(text, parsed.ToString());
        Assert.Equal(new string('T', 64), ResourceName.Parse($"xrn:cn:shop:{new string('T', 64)}:order/1").Tenant);
    }

    [Theory]
    [InlineData("xrn:cn:shop:-acme:order/1")]
    [InlineData("xrn:cn:shop:.acme:order/1")]
    [InlineData("xrn:cn:shop:_acme:order/1")]
    [InlineData("xrn:cn:shop:ac me:order/1")]
    [InlineData("xrn:cn:shop:ac/me:order/1")]
    [InlineData("xrn:CN:shop:acme:order/1")]
    [InlineData("xrn:cn:shop:acme:Order/1")]
    public void ResourceName_RejectsSegmentsOutsideTheGrammar(string text)
    {
        Assert.False(ResourceName.TryParse(text, out _));
        Assert.Throws<ArgumentException>(() => ResourceName.Parse(text));
        Assert.False(ResourceName.TryParse($"xrn:cn:shop:{new string('T', 65)}:order/1", out _));
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
        var context = App.CreateContext(_services, CancellationToken.None);

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
    public void PermissionRegistry_AddRange_RegistersAllOrNothing()
    {
        var registry = new PermissionRegistry(capacity: 3);
        static PermissionDefinition Define(string code) => new(code, code);

        // 同批重复、与已登记的重复、整批超出容量：返回第一项的失败，注册表不变
        Assert.Equal(KernelErrors.DuplicatePermission, registry.AddRange([Define("orders.read"), Define("orders.read")]).Error?.Code);
        Assert.Equal(0, registry.Count);
        Assert.True(registry.AddRange([Define("orders.read"), Define("orders.write")]).IsSuccess);
        var duplicated = registry.AddRange([Define("orders.delete"), Define("orders.write")]);
        Assert.Equal(KernelErrors.DuplicatePermission, duplicated.Error?.Code);
        Assert.Equal("orders.write", duplicated.Error?.Details?["code"]);
        Assert.False(registry.Find("orders.delete").HasValue);
        Assert.Equal(KernelErrors.PermissionRegistryFull, registry.AddRange([Define("orders.delete"), Define("orders.export")]).Error?.Code);
        Assert.Equal(2, registry.Count);

        Assert.True(registry.AddRange([]).IsSuccess);
        Assert.Throws<ArgumentException>(() => registry.AddRange([Define("orders.delete"), null!]));
        Assert.Equal(2, registry.Count);
    }

    [Fact]
    public async Task AuthorizationResult_ConvertsToResult()
    {
        Assert.True(AuthorizationResult.Allow().ToResult().IsSuccess);
        Assert.Equal("SECURITY_DENIED_002", AuthorizationResult.Deny(Error.Literal("SECURITY_DENIED_002", "Denied.")).ToResult().Error!.Code);
        Assert.False(default(AuthorizationResult).IsAllowed);

        IPolicy policy = new DenyAll();
        var result = (await policy.EvaluateAsync(new AuthorizationRequest(SecurityContext.Anonymous, "orders.create"), CancellationToken.None)).ValueOrThrow();
        Assert.False(result.IsAllowed);
        Assert.Throws<ArgumentException>(() => new AuthorizationRequest(SecurityContext.Anonymous, " "));
    }

    /// <summary>
    /// 拒绝一切请求的策略。
    /// </summary>
    private sealed class DenyAll : IPolicy
    {
        /// <summary>
        /// 策略名。
        /// </summary>
        public string Name => "deny-all";

        /// <summary>
        /// 总是拒绝。
        /// </summary>
        public ValueTask<Result<AuthorizationResult>> EvaluateAsync(AuthorizationRequest request, CancellationToken cancellationToken)
            => ValueTask.FromResult(Result.Success(AuthorizationResult.Deny(Error.Literal("SECURITY_DENIED_002", "deny-all"))));
    }
}
