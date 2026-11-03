// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.InMemory;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Hosting.AspNetCore.Tests;

/// <summary>
/// 端点元数据上的授权要求在宿主建请求管道时核对（ADR-0080）
/// </summary>
/// <remarks>
/// 装配了授权能力时，<c>RequireXiHanPermission</c> 的权限码未登记、<c>RequireXiHanPolicy</c> 的策略未注册，宿主启动即失败，不等到调用时 404；没有装配授权能力时不判定也不核对（见 <see cref="WebHostTests"/>）
/// </remarks>
public sealed class EndpointRequirementTests
{
    /// <summary>
    /// 按给定名字放行一切的策略
    /// </summary>
    private sealed class NamedPolicy(string name) : IPolicy
    {
        /// <inheritdoc />
        public string Name => name;

        /// <inheritdoc />
        public ValueTask<Result<AuthorizationResult>> EvaluateAsync(AuthorizationRequest request, CancellationToken cancellationToken) => ValueTask.FromResult(Result.Success(AuthorizationResult.Allow()));
    }

    /// <summary>
    /// 装配内存安全与一个登记 <c>orders.read</c> 的特性，可再注册策略
    /// </summary>
    private static WebApplication Build(Action<IServiceCollection>? services = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        services?.Invoke(builder.Services);
        builder.AddXiHan(static xihan => xihan.UseFeature<InMemorySecurityFeature>().UseFeature(new StubFeature(TestManifests.Create("xihan.app.orders", requires: ["security.authorization@1"]))
        {
            OnConfigure = static ctx => ctx.AddPermission(new PermissionDefinition("orders.read", "Read orders")),
        }));
        return builder.Build();
    }

    [Fact]
    public async Task EndpointRequiringAnUnregisteredPermissionOrPolicy_FailsTheHostStart()
    {
        await using var app = Build();
        app.UseXiHan();
        app.MapGet("/orders", static () => "ok").RequireXiHanPermission("orders.read");
        app.MapGet("/me", static () => "ok").RequireXiHanAuthentication();
        app.MapGet("/orders/purge", static () => "ok").RequireXiHanPermission("orders.purge");
        app.MapGet("/admin", static () => "ok").RequireXiHanPolicy("admins");

        // 建请求管道时路由都已映射：一次列出全部，宿主启动失败
        var exception = await Assert.ThrowsAsync<FrameworkException>(() => app.StartAsync());

        Assert.Equal(SecurityErrors.UnregisteredPolicy, exception.Code);
        Assert.Contains("requires permission 'orders.purge', which is not registered", exception.Message, StringComparison.Ordinal);
        Assert.Contains("requires policy 'admins', which is not registered", exception.Message, StringComparison.Ordinal);
        Assert.Contains("/orders/purge", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("'orders.read'", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("/me", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EndpointsMappedAfterUseXiHan_AreCheckedToo()
    {
        await using var app = Build();
        app.UseXiHan();
        // 映射发生在 UseXiHan 之后、启动之前：核对在建请求管道时进行，同样看得到
        app.MapGet("/late", static () => "ok").RequireXiHanPolicy("late-policy");

        var exception = await Assert.ThrowsAsync<FrameworkException>(() => app.StartAsync());

        Assert.Equal(SecurityErrors.UnregisteredPolicy, exception.Code);
        Assert.Contains("'late-policy'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegisteredPermissionsAndPolicies_Start_AndAreEnforced()
    {
        await using var app = Build(static s => s.AddSingleton<IPolicy>(new NamedPolicy("admins")));
        app.UseXiHan();
        app.MapGet("/orders", static () => "ok").RequireXiHanPermission("orders.read");
        app.MapGet("/admin", static () => "ok").RequireXiHanPolicy("admins");
        await app.StartAsync();
        using var client = app.GetTestClient();

        // 启动通过；要求照常判定：匿名调用权限端点 401，命名策略放行
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/admin")).StatusCode);
        await app.StopAsync();
    }
}
