// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions.Authentication;

namespace XiHan.Framework.Security.Abstractions.Tests;

/// <summary>
/// 凭据解析、认证中间件与安全选项的契约测试。
/// </summary>
public sealed class AuthenticationTests
{
    /// <summary>
    /// 记录是否到达下游的终端步骤。
    /// </summary>
    private static PipelineStep Handler(bool[] reached) => _ =>
    {
        reached[0] = true;
        return ValueTask.CompletedTask;
    };

    [Theory]
    [InlineData("Bearer abc.def", "Bearer", "abc.def")]
    [InlineData("  ApiKey   key-1  ", "ApiKey", "key-1")]
    public void Credential_TryParse_SplitsSchemeAndValue(string header, string scheme, string value)
    {
        Assert.True(AuthenticationCredential.TryParse(header, out var credential));
        Assert.Equal(scheme, credential.Scheme);
        Assert.Equal(value, credential.Value);
        Assert.Equal($"{scheme} ***", credential.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bearer")]
    [InlineData("Bearer ")]
    [InlineData(" token")]
    public void Credential_TryParse_RejectsMalformed(string? header)
    {
        Assert.False(AuthenticationCredential.TryParse(header, out _));
        Assert.Throws<ArgumentException>(static () => new AuthenticationCredential("", "x"));
        Assert.Throws<ArgumentException>(static () => new AuthenticationCredential("Bearer", ""));
    }

    [Fact]
    public async Task NoCredential_StaysAnonymous_AndContinues()
    {
        var context = SecurityHosts.Context(SecurityHosts.Services());
        var reached = new bool[1];

        await new AuthenticationMiddleware(new SecurityOptions()).InvokeAsync(context, Handler(reached));

        Assert.True(reached[0]);
        Assert.False(context.GetSecurity().Principal.IsAuthenticated);
    }

    [Fact]
    public async Task NoCredential_WithRequireAuthentication_IsDenied()
    {
        var options = new SecurityOptions { RequireAuthentication = true };
        var context = SecurityHosts.Context(SecurityHosts.Services(options: options));
        var reached = new bool[1];

        await new AuthenticationMiddleware(options).InvokeAsync(context, Handler(reached));

        Assert.False(reached[0]);
        Assert.Equal(SecurityErrors.NotAuthenticated, context.GetOutcome().ValueOrThrow().Error?.Code);
    }

    [Fact]
    public async Task UnsupportedScheme_IsRejected()
    {
        var context = SecurityHosts.Context(SecurityHosts.Services(static s => s.AddSingleton<IAuthenticationHandler>(new StaticHandler("Bearer", static _ => Result.Success(SecurityHosts.Alice())))));
        context.SetCredential(new AuthenticationCredential("Basic", "dXNlcjpwYXNz"));
        var reached = new bool[1];

        await new AuthenticationMiddleware(new SecurityOptions()).InvokeAsync(context, Handler(reached));

        Assert.False(reached[0]);
        Assert.Equal(SecurityErrors.UnsupportedScheme, context.GetOutcome().ValueOrThrow().Error?.Code);
    }

    [Fact]
    public async Task HandlerFailure_BecomesOutcome_AndSuccessSetsPrincipal_SchemeCaseInsensitive()
    {
        var services = SecurityHosts.Services(static s => s.AddSingleton<IAuthenticationHandler>(new StaticHandler("Bearer", static c =>
            c.Value == "good" ? Result.Success(SecurityHosts.Alice("admin")) : Result.Failure<Principal>(Error.Literal(SecurityErrors.InvalidCredential, "Bad token.")))));

        var bad = SecurityHosts.Context(services);
        bad.SetCredential(new AuthenticationCredential("bearer", "bad"));
        var reachedBad = new bool[1];
        await new AuthenticationMiddleware(new SecurityOptions()).InvokeAsync(bad, Handler(reachedBad));
        Assert.False(reachedBad[0]);
        Assert.Equal(SecurityErrors.InvalidCredential, bad.GetOutcome().ValueOrThrow().Error?.Code);

        var good = SecurityHosts.Context(services);
        good.SetCredential(new AuthenticationCredential("BEARER", "good"));
        good.SetTenant(new TenantContext("acme"));
        var reachedGood = new bool[1];
        await new AuthenticationMiddleware(new SecurityOptions()).InvokeAsync(good, Handler(reachedGood));
        Assert.True(reachedGood[0]);
        Assert.Equal("alice", good.GetSecurity().Principal.Id);
        Assert.True(good.GetSecurity().Principal.IsAuthenticated);
        Assert.Equal("acme", good.GetSecurity().Tenant?.Id);
        Assert.Equal("BEARER ***", good.GetCredential().ValueOrThrow().ToString());
    }

    [Fact]
    public void Options_FromConfiguration_ParsesAndRejectsInvalid()
    {
        // 用内存键值对构造配置
        static IConfiguration Build(params (string Key, string Value)[] values)
            => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

        var options = SecurityOptions.FromConfiguration(Build(("XiHan:Security:RequireAuthentication", "true"), ("XiHan:Security:RoleClaim", "roles")));
        Assert.True(options.RequireAuthentication);
        Assert.Equal("roles", options.RoleClaim);
        Assert.False(SecurityOptions.FromConfiguration(Build()).RequireAuthentication);
        Assert.Equal("role", SecurityOptions.FromConfiguration(Build()).RoleClaim);
        Assert.Throws<ArgumentException>(() => SecurityOptions.FromConfiguration(Build(("XiHan:Security:RequireAuthentication", "sure"))));
        Assert.Throws<ArgumentException>(() => SecurityOptions.FromConfiguration(Build(("XiHan:Security:RoleClaim", " "))));
    }
}
