// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Tokens;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.InMemory;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Security.OpenIdConnect.Tests;

public sealed class OpenIdConnectAuthenticationTests
{
    /// <summary>
    /// 严格选项：校验受众，时钟偏移为零。
    /// </summary>
    private static readonly OpenIdConnectOptions _options = new() { Authority = FakeIssuer.Authority, Audience = FakeIssuer.Audience, ClockSkew = TimeSpan.Zero };

    /// <summary>
    /// 用桩签发者组装处理器；时钟可控，缓存实例一并返回供断言。
    /// </summary>
    private static (OpenIdConnectAuthenticationHandler Handler, OpenIdConnectMetadataCache Cache, FakeTimeProvider Clock) Create(FakeIssuer issuer, OpenIdConnectOptions? options = null)
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var cache = new OpenIdConnectMetadataCache(options ?? _options, new HttpClient(issuer), clock, NullLoggerFactory.Instance);
        return (new OpenIdConnectAuthenticationHandler(options ?? _options, cache), cache, clock);
    }

    /// <summary>
    /// 认证 Bearer 令牌。
    /// </summary>
    private static async Task<Result<Principal>> AuthenticateAsync(OpenIdConnectAuthenticationHandler handler, string token)
        => await handler.AuthenticateAsync(new AuthenticationCredential("Bearer", token), TestInvocationContext.Create(), CancellationToken.None);

    [Fact]
    public async Task ValidToken_BecomesPrincipalWithRawClaims_AndMetadataIsFetchedOnce()
    {
        using var issuer = new FakeIssuer();
        var (handler, cache, _) = Create(issuer);

        var principal = (await AuthenticateAsync(handler, issuer.CreateToken(claims: [("role", "editor"), ("role", "auditor"), ("name", "Alice"), ("tenant", "acme")]))).ValueOrThrow();
        Assert.True((await AuthenticateAsync(handler, issuer.CreateToken())).IsSuccess);

        Assert.Equal("alice", principal.Id);
        Assert.Equal("Bearer", principal.AuthenticationScheme);
        Assert.True(principal.IsAuthenticated);
        Assert.Equal("Alice", principal.DisplayName);
        Assert.Equal(["editor", "auditor"], principal.ClaimValues("role"));
        Assert.Equal(["acme"], principal.ClaimValues("tenant"));
        Assert.Contains(principal.Claims, static c => c.Key == "iss" && c.Value == FakeIssuer.Authority);
        Assert.Equal(1, issuer.MetadataRequests);
        Assert.Equal(1, issuer.JwksRequests);
        Assert.Equal(FakeIssuer.Authority, cache.Current?.Issuer);
        Assert.Equal(new Uri(FakeIssuer.Authority + "/jwks"), cache.Current?.JwksUri);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("wrong-audience")]
    [InlineData("wrong-issuer")]
    [InlineData("no-subject")]
    [InlineData("foreign-key")]
    [InlineData("garbage")]
    public async Task InvalidTokens_AreDenied(string scenario)
    {
        using var issuer = new FakeIssuer();
        using var foreign = RSA.Create(2048);
        var (handler, _, _) = Create(issuer);
        var token = scenario switch
        {
            "expired" => issuer.CreateToken(lifetime: TimeSpan.FromMinutes(-1)),
            "wrong-audience" => issuer.CreateToken(audience: "other"),
            "wrong-issuer" => issuer.CreateToken(issuer: "https://evil.test"),
            "no-subject" => issuer.CreateToken(subject: null),
            "foreign-key" => issuer.CreateToken(key: new RsaSecurityKey(foreign) { KeyId = "foreign" }),
            _ => "not.a.token",
        };

        var result = await AuthenticateAsync(handler, token);

        Assert.True(result.IsFailure);
        Assert.Equal(SecurityErrors.InvalidCredential, result.Error?.Code);
    }

    [Fact]
    public async Task UnreadableToken_IsDeniedWithoutContactingIssuer()
    {
        using var issuer = new FakeIssuer();
        var (handler, _, _) = Create(issuer);

        var result = await AuthenticateAsync(handler, "not.a.token");

        Assert.Equal(SecurityErrors.InvalidCredential, result.Error?.Code);
        Assert.Equal(0, issuer.MetadataRequests);
    }

    [Fact]
    public async Task UnknownSigningKey_RefreshesOnce_ThenRateLimitsFurtherRefreshes()
    {
        using var issuer = new FakeIssuer();
        var (handler, _, clock) = Create(issuer);
        Assert.True((await AuthenticateAsync(handler, issuer.CreateToken())).IsSuccess);

        // 签发者轮换密钥：新 kid 未知 → 按需刷新一次后通过
        issuer.Rotate();
        Assert.True((await AuthenticateAsync(handler, issuer.CreateToken())).IsSuccess);
        Assert.Equal(2, issuer.JwksRequests);

        // 最短刷新间隔内再次轮换：不再刷新，令牌被拒
        issuer.Rotate();
        Assert.Equal(SecurityErrors.InvalidCredential, (await AuthenticateAsync(handler, issuer.CreateToken())).Error?.Code);
        Assert.Equal(2, issuer.JwksRequests);

        // 间隔过后再试：刷新并通过
        clock.Advance(_options.MinimumRefreshInterval);
        Assert.True((await AuthenticateAsync(handler, issuer.CreateToken())).IsSuccess);
        Assert.Equal(3, issuer.JwksRequests);
    }

    [Fact]
    public async Task IssuerOutage_KeepsCachedKeys_AndRetriesAfterMinimumInterval()
    {
        using var issuer = new FakeIssuer();
        var (handler, cache, clock) = Create(issuer);
        Assert.True((await AuthenticateAsync(handler, issuer.CreateToken())).IsSuccess);
        var retrievedAt = cache.Current!.RetrievedAt;

        // 自动刷新到期而签发者下线：沿用旧密钥
        issuer.Offline = true;
        clock.Advance(_options.RefreshInterval);
        Assert.True((await AuthenticateAsync(handler, issuer.CreateToken())).IsSuccess);
        Assert.Equal(retrievedAt, cache.Current!.RetrievedAt);

        // 恢复后最短间隔内不重试；间隔过后刷新成功
        issuer.Offline = false;
        Assert.True((await AuthenticateAsync(handler, issuer.CreateToken())).IsSuccess);
        Assert.Equal(1, issuer.MetadataRequests);
        clock.Advance(_options.MinimumRefreshInterval);
        Assert.True((await AuthenticateAsync(handler, issuer.CreateToken())).IsSuccess);
        Assert.Equal(2, issuer.MetadataRequests);
        Assert.True(cache.Current!.RetrievedAt > retrievedAt);
    }

    [Fact]
    public async Task UnreachableIssuerWithoutCache_IsUnavailable_AndRecoversAfterRetryDelay()
    {
        using var issuer = new FakeIssuer { Offline = true };
        var (handler, cache, clock) = Create(issuer);
        var token = issuer.CreateToken();

        var result = await AuthenticateAsync(handler, token);
        Assert.Equal(SecurityErrors.IssuerUnavailable, result.Error?.Code);
        Assert.Null(cache.Current);

        // 退避期内不再打签发者
        issuer.Offline = false;
        Assert.Equal(SecurityErrors.IssuerUnavailable, (await AuthenticateAsync(handler, token)).Error?.Code);
        Assert.Equal(0, issuer.MetadataRequests);

        clock.Advance(OpenIdConnectMetadataCache.FailureRetryDelay);
        Assert.True((await AuthenticateAsync(handler, token)).IsSuccess);
        Assert.Equal(1, issuer.MetadataRequests);
    }

    [Fact]
    public async Task MalformedDiscoveryDocument_IsUnavailable()
    {
        // 空的 issuer 让发现文档不合法：与网络故障同样按不可用处理，不缓存
        using var issuer = new FakeIssuer { Issuer = "" };
        var (handler, cache, _) = Create(issuer);

        var result = await AuthenticateAsync(handler, issuer.CreateToken(issuer: FakeIssuer.Authority));

        Assert.Equal(SecurityErrors.IssuerUnavailable, result.Error?.Code);
        Assert.Null(cache.Current);
        Assert.Equal(1, issuer.MetadataRequests);
    }

    [Fact]
    public async Task IssuerOverride_ReplacesDiscoveredIssuer()
    {
        using var issuer = new FakeIssuer { Issuer = "https://discovered.test" };
        var (handler, _, _) = Create(issuer, _options with { Issuer = "https://configured.test" });

        Assert.True((await AuthenticateAsync(handler, issuer.CreateToken(issuer: "https://configured.test"))).IsSuccess);
        Assert.Equal(SecurityErrors.InvalidCredential, (await AuthenticateAsync(handler, issuer.CreateToken(issuer: "https://discovered.test"))).Error?.Code);
    }

    [Fact]
    public async Task ConcurrentFirstUse_FetchesMetadataOnce()
    {
        using var issuer = new FakeIssuer();
        var (handler, _, _) = Create(issuer);
        var token = issuer.CreateToken();

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => AuthenticateAsync(handler, token)));

        Assert.All(results, static r => Assert.True(r.IsSuccess));
        Assert.Equal(1, issuer.MetadataRequests);
        Assert.Equal(1, issuer.JwksRequests);
    }

    [Fact]
    public void Options_FromConfiguration_RequireAuthority_AndHttps_AndParseRest()
    {
        // 用内存键值对构造配置
        static IConfiguration Build(params (string Key, string Value)[] values)
            => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();
        const string Prefix = "XiHan:Security:OpenIdConnect:";

        var options = OpenIdConnectOptions.FromConfiguration(Build(
            (Prefix + "Authority", "https://login.test/realms/shop/"),
            (Prefix + "Audience", "api"),
            (Prefix + "ClockSkewSeconds", "5"),
            (Prefix + "RefreshIntervalSeconds", "600"),
            (Prefix + "MinimumRefreshIntervalSeconds", "30"),
            (Prefix + "BackchannelTimeoutSeconds", "3"),
            (Prefix + "NameClaim", "uid")));
        Assert.Equal("https://login.test/realms/shop/", options.Authority);
        Assert.Null(options.Issuer);
        Assert.Equal("api", options.Audience);
        Assert.True(options.RequireHttpsMetadata);
        Assert.Equal(TimeSpan.FromSeconds(5), options.ClockSkew);
        Assert.Equal(TimeSpan.FromMinutes(10), options.RefreshInterval);
        Assert.Equal(TimeSpan.FromSeconds(30), options.MinimumRefreshInterval);
        Assert.Equal(TimeSpan.FromSeconds(3), options.BackchannelTimeout);
        Assert.Equal("uid", options.NameClaim);
        Assert.Equal(new Uri("https://login.test/realms/shop/.well-known/openid-configuration"), options.ResolveMetadataAddress());

        Assert.Equal(new Uri("https://meta.test/oidc"), OpenIdConnectOptions.FromConfiguration(Build((Prefix + "Authority", "https://login.test"), (Prefix + "MetadataAddress", "https://meta.test/oidc"))).ResolveMetadataAddress());
        Assert.Equal(new Uri("http://localhost:8080/.well-known/openid-configuration"), OpenIdConnectOptions.FromConfiguration(Build((Prefix + "Authority", "http://localhost:8080"), (Prefix + "RequireHttpsMetadata", "false"))).ResolveMetadataAddress());

        Assert.Throws<ArgumentException>(() => OpenIdConnectOptions.FromConfiguration(Build()));
        Assert.Throws<ArgumentException>(() => OpenIdConnectOptions.FromConfiguration(Build((Prefix + "Authority", "not a uri"))));
        Assert.Throws<InvalidOperationException>(() => OpenIdConnectOptions.FromConfiguration(Build((Prefix + "Authority", "http://login.test"))));
        Assert.Throws<ArgumentException>(() => OpenIdConnectOptions.FromConfiguration(Build((Prefix + "Authority", "https://login.test"), (Prefix + "RequireHttpsMetadata", "maybe"))));
        Assert.Throws<ArgumentException>(() => OpenIdConnectOptions.FromConfiguration(Build((Prefix + "Authority", "https://login.test"), (Prefix + "ClockSkewSeconds", "-1"))));
        Assert.Throws<ArgumentOutOfRangeException>(() => OpenIdConnectOptions.FromConfiguration(Build((Prefix + "Authority", "https://login.test"), (Prefix + "RefreshIntervalSeconds", "0"))));
    }

    [Fact]
    public async Task Feature_TakesOverAuthentication_WhileInMemoryKeepsAuthorization()
    {
        using var issuer = new FakeIssuer();
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemorySecurityFeature>().UseFeature<OpenIdConnectSecurityFeature>().UseFeature(new StubFeature(TestManifests.Create("xihan.app.orders", requires: ["security.authorization@1"]))
            {
                OnConfigure = static ctx => ctx.AddPermission(new PermissionDefinition("orders.read", "读订单")),
            }),
            [
                new("XiHan:Security:OpenIdConnect:Authority", FakeIssuer.Authority),
                new("XiHan:Security:OpenIdConnect:Audience", FakeIssuer.Audience),
                new("XiHan:Security:InMemory:Grants:role:editor", "orders.read"),
            ],
            // 预先注册的缓存替代特性的默认 HTTP 客户端；元数据来自桩签发者
            services => services.AddSingleton(new OpenIdConnectMetadataCache(_options, new HttpClient(issuer), TimeProvider.System, NullLoggerFactory.Instance)));

        Assert.IsType<OpenIdConnectAuthenticationHandler>(Assert.Single(host.Services.GetServices<IAuthenticationHandler>()));
        Assert.Equal(1, host.App.DescribePipeline().Split(nameof(AuthenticationMiddleware)).Length - 1);
        Assert.Contains(nameof(AuthorizationMiddleware), host.App.DescribePipeline(), StringComparison.Ordinal);
        Assert.Equal(["security.authentication"], host.App.Graph["xihan.security.openidconnect"].SelectedCapabilities);

        await using var scope = host.Services.CreateAsyncScope();
        var context = App.CreateContext(scope.ServiceProvider, CancellationToken.None);
        context.SetCredential(new AuthenticationCredential("Bearer", issuer.CreateToken(claims: [("role", "editor")])));
        context.RequireAuthorization(AuthorizationRequirement.Permission("orders.read"));
        Principal? seen = null;
        var result = await host.App.InvokeAsync(context, ctx =>
        {
            seen = ctx.GetSecurity().Principal;
            return ValueTask.CompletedTask;
        });

        Assert.True(result.IsSuccess, result.ToString());
        Assert.Equal("alice", seen?.Id);
    }
}
