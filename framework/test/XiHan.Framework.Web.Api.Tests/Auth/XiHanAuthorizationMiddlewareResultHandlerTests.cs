// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using System.Text.Json;
using XiHan.Framework.Web.Api.Auth;
using XiHan.Framework.Web.Api.Constants;
using XiHan.Framework.Web.Api.Extensions.DependencyInjection;

namespace XiHan.Framework.Web.Api.Tests.Auth;

/// <summary>
/// 授权结果处理器：带失败原因的 403 写统一信封，其余照旧
/// </summary>
/// <remarks>
/// 授权处理器能区分「权限不足」与「当前条件下不允许此操作」，但 ASP.NET Core 的缺省处理只回一个空的 403。
/// 这里锁定：有失败原因才写信封、消息取第一条非空原因、带 traceId；没有原因的拒绝、成功与 401 都交给缺省处理。
/// </remarks>
public sealed class XiHanAuthorizationMiddlewareResultHandlerTests
{
    /// <summary>
    /// 带失败原因的拒绝：403、统一信封、消息取第一条非空原因、带 traceId，不再走认证方案的 Forbid
    /// </summary>
    [Fact]
    public async Task Forbidden_WithReasons_WritesEnvelope()
    {
        var (context, authentication, body) = CreateContext();
        context.Items[XiHanWebApiConstants.TraceIdItemKey] = "trace-1";
        var failure = AuthorizationFailure.Failed(
        [
            new AuthorizationFailureReason(new PassThroughHandler(), " "),
            new AuthorizationFailureReason(new PassThroughHandler(), "当前条件下不允许此操作")
        ]);
        var nextCalled = false;

        await new XiHanAuthorizationMiddlewareResultHandler().HandleAsync(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, context, CreatePolicy(), PolicyAuthorizationResult.Forbid(failure));

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Equal(0, authentication.ForbidCount);
        using var json = JsonDocument.Parse(body.ToArray());
        Assert.Equal(403, json.RootElement.GetProperty("code").GetInt32());
        Assert.Equal("当前条件下不允许此操作", json.RootElement.GetProperty("message").GetString());
        Assert.Equal("trace-1", json.RootElement.GetProperty("traceId").GetString());
    }

    /// <summary>
    /// 没有失败原因的拒绝：交给缺省处理（走认证方案的 Forbid），不写信封
    /// </summary>
    [Fact]
    public async Task Forbidden_WithoutReasons_UsesDefault()
    {
        var (context, authentication, body) = CreateContext();

        await new XiHanAuthorizationMiddlewareResultHandler().HandleAsync(_ => Task.CompletedTask, context, CreatePolicy(), PolicyAuthorizationResult.Forbid());

        Assert.Equal(1, authentication.ForbidCount);
        Assert.Equal(0, body.Length);
    }

    /// <summary>
    /// 授权通过：继续管道
    /// </summary>
    [Fact]
    public async Task Success_CallsNext()
    {
        var (context, _, _) = CreateContext();
        var nextCalled = false;

        await new XiHanAuthorizationMiddlewareResultHandler().HandleAsync(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, context, CreatePolicy(), PolicyAuthorizationResult.Success());

        Assert.True(nextCalled);
    }

    /// <summary>
    /// 注册：替换 ASP.NET Core 的缺省处理器；宿主先注册了自己的处理器时不覆盖
    /// </summary>
    [Fact]
    public void AddXiHanWebApiAuth_ReplacesOnlyTheDefaultHandler()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["XiHan:Web:Api:Auth:RequireAuthenticatedUser"] = "false" })
            .Build();

        var services = new ServiceCollection().AddLogging();
        services.AddXiHanWebApiAuth(configuration);
        Assert.Equal(
            typeof(XiHanAuthorizationMiddlewareResultHandler),
            services.Single(descriptor => descriptor.ServiceType == typeof(IAuthorizationMiddlewareResultHandler)).ImplementationType);

        var custom = new ServiceCollection().AddLogging();
        custom.AddSingleton<IAuthorizationMiddlewareResultHandler, CustomResultHandler>();
        custom.AddXiHanWebApiAuth(configuration);
        Assert.Equal(
            typeof(CustomResultHandler),
            custom.Single(descriptor => descriptor.ServiceType == typeof(IAuthorizationMiddlewareResultHandler)).ImplementationType);
    }

    private static (DefaultHttpContext Context, RecordingAuthenticationService Authentication, MemoryStream Body) CreateContext()
    {
        var authentication = new RecordingAuthenticationService();
        var body = new MemoryStream();
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddSingleton<IAuthenticationService>(authentication).BuildServiceProvider()
        };
        context.Response.Body = body;
        return (context, authentication, body);
    }

    private static AuthorizationPolicy CreatePolicy()
    {
        return new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    }

    /// <summary>
    /// 只用来构造失败原因的处理器
    /// </summary>
    private sealed class PassThroughHandler : IAuthorizationHandler
    {
        public Task HandleAsync(AuthorizationHandlerContext context)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 宿主自带的结果处理器
    /// </summary>
    private sealed class CustomResultHandler : IAuthorizationMiddlewareResultHandler
    {
        public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
        {
            return next(context);
        }
    }

    /// <summary>
    /// 记录 Forbid 调用的认证服务
    /// </summary>
    private sealed class RecordingAuthenticationService : IAuthenticationService
    {
        public int ForbidCount { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            ForbidCount++;
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }
    }
}
