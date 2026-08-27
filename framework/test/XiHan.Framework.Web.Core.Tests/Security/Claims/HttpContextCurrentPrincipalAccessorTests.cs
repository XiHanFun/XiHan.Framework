// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Security.Claims;
using XiHan.Framework.Web.Core.Security.Claims;
using XiHan.Framework.Web.Core.Tests.Infrastructure;

namespace XiHan.Framework.Web.Core.Tests.Security.Claims;

/// <summary>
/// 基于 HttpContext 的当前主体访问器测试
/// </summary>
/// <remarks>
/// ICurrentUser 的取值全靠这个访问器，两条契约必须成立：
/// 没有 HttpContext（后台任务、宿主启动）时给出匿名空主体而不是 null，否则下游整片 NullReference；
/// Change 是带作用域的临时切换，离开作用域必须还原，否则会出现"用别人的身份继续跑业务"的越权。
/// </remarks>
public class HttpContextCurrentPrincipalAccessorTests
{
    /// <summary>
    /// 访问器满足当前主体访问器契约，并按作用域生命周期注册
    /// </summary>
    [Fact]
    public void Accessor_ImplementsScopedCurrentPrincipalContract()
    {
        var accessor = new HttpContextCurrentPrincipalAccessor(new FakeHttpContextAccessor());

        Assert.IsAssignableFrom<ICurrentPrincipalAccessor>(accessor);
        Assert.IsAssignableFrom<CurrentPrincipalAccessorBase>(accessor);
        Assert.IsAssignableFrom<IScopedDependency>(accessor);
    }

    /// <summary>
    /// 没有 HttpContext 时返回未认证的空主体，而不是 null
    /// </summary>
    [Fact]
    public void Principal_WithoutHttpContext_ReturnsAnonymousPrincipal()
    {
        var accessor = new HttpContextCurrentPrincipalAccessor(new FakeHttpContextAccessor());

        var principal = accessor.Principal;

        Assert.NotNull(principal);
        Assert.Empty(principal.Claims);
        Assert.NotNull(principal.Identity);
        Assert.False(principal.Identity.IsAuthenticated);
    }

    /// <summary>
    /// 有 HttpContext 时直接返回其上的 User 实例
    /// </summary>
    [Fact]
    public void Principal_WithHttpContextUser_ReturnsThatUser()
    {
        var user = CreateUser("u-1");
        var accessor = CreateAccessor(user, out _);

        Assert.Same(user, accessor.Principal);
    }

    /// <summary>
    /// HttpContext 上没有认证结果时返回其自带的空主体，声明为空
    /// </summary>
    [Fact]
    public void Principal_WhenHttpContextHasNoAuthenticatedUser_ReturnsEmptyPrincipal()
    {
        var accessor = new HttpContextCurrentPrincipalAccessor(
            new FakeHttpContextAccessor { HttpContext = new DefaultHttpContext() });

        Assert.Empty(accessor.Principal.Claims);
    }

    /// <summary>
    /// 未调用 Change 时始终跟随 HttpContext，中途换上下文能立即反映
    /// </summary>
    [Fact]
    public void Principal_WhenHttpContextReplaced_FollowsNewContext()
    {
        var contextAccessor = new FakeHttpContextAccessor();
        var accessor = new HttpContextCurrentPrincipalAccessor(contextAccessor);

        Assert.Empty(accessor.Principal.Claims);

        var user = CreateUser("u-2");
        contextAccessor.HttpContext = new DefaultHttpContext { User = user };

        Assert.Same(user, accessor.Principal);
    }

    /// <summary>
    /// Change 作用域内返回切换后的主体，离开作用域还原成 HttpContext 上的用户
    /// </summary>
    [Fact]
    public void Change_WithinScope_OverridesPrincipalAndRestoresAfterDispose()
    {
        var user = CreateUser("u-1");
        var accessor = CreateAccessor(user, out _);
        var impersonated = CreateUser("u-9");

        using (accessor.Change(impersonated))
        {
            Assert.Same(impersonated, accessor.Principal);
        }

        Assert.Same(user, accessor.Principal);
    }

    /// <summary>
    /// 嵌套切换按后进先出逐层还原
    /// </summary>
    [Fact]
    public void Change_Nested_RestoresInReverseOrder()
    {
        var user = CreateUser("u-1");
        var accessor = CreateAccessor(user, out _);
        var outer = CreateUser("u-outer");
        var inner = CreateUser("u-inner");

        using (accessor.Change(outer))
        {
            Assert.Same(outer, accessor.Principal);

            using (accessor.Change(inner))
            {
                Assert.Same(inner, accessor.Principal);
            }

            Assert.Same(outer, accessor.Principal);
        }

        Assert.Same(user, accessor.Principal);
    }

    /// <summary>
    /// 切换后的主体会随执行上下文流入子任务
    /// </summary>
    [Fact]
    public async Task Change_FlowsIntoNestedAsyncOperation()
    {
        var user = CreateUser("u-1");
        var accessor = CreateAccessor(user, out _);
        var impersonated = CreateUser("u-9");

        using (accessor.Change(impersonated))
        {
            var captured = await Task.Run(() => accessor.Principal, TestContext.Current.CancellationToken);

            Assert.Same(impersonated, captured);
        }

        Assert.Same(user, accessor.Principal);
    }

    /// <summary>
    /// 切换句柄重复释放不会把身份还原到错误的层级
    /// </summary>
    [Fact]
    public void Change_DisposedTwice_StillRestoresOriginalPrincipal()
    {
        var user = CreateUser("u-1");
        var accessor = CreateAccessor(user, out _);
        var impersonated = CreateUser("u-9");

        var scope = accessor.Change(impersonated);
        scope.Dispose();
        scope.Dispose();

        Assert.Same(user, accessor.Principal);
    }

    /// <summary>
    /// 切换只影响访问器自身，不会改写 HttpContext 上的 User
    /// </summary>
    [Fact]
    public void Change_DoesNotMutateHttpContextUser()
    {
        var user = CreateUser("u-1");
        var accessor = CreateAccessor(user, out var httpContext);
        var impersonated = CreateUser("u-9");

        using (accessor.Change(impersonated))
        {
            Assert.Same(user, httpContext.User);
        }

        Assert.Same(user, httpContext.User);
    }

    /// <summary>
    /// 构造一个绑定到给定用户的访问器
    /// </summary>
    /// <param name="user">HttpContext 上的用户</param>
    /// <param name="httpContext">构造出的请求上下文</param>
    /// <returns>访问器实例</returns>
    private static HttpContextCurrentPrincipalAccessor CreateAccessor(ClaimsPrincipal user, out DefaultHttpContext httpContext)
    {
        httpContext = new DefaultHttpContext { User = user };
        return new HttpContextCurrentPrincipalAccessor(new FakeHttpContextAccessor { HttpContext = httpContext });
    }

    /// <summary>
    /// 构造一个带用户标识声明的已认证主体
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <returns>主体实例</returns>
    private static ClaimsPrincipal CreateUser(string userId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity([new Claim(XiHanClaimTypes.UserId, userId)], "TestScheme"));
    }
}
