// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.InMemory;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Security.Jwt.Tests;

/// <summary>
/// JWT 处理器的令牌校验、选项解析与特性装配测试。
/// </summary>
public sealed class JwtAuthenticationTests
{
    /// <summary>
    /// 测试用对称密钥；满足最小长度。
    /// </summary>
    private const string Secret = "unit-test-secret-key-with-at-least-32-bytes!";

    /// <summary>
    /// 测试用签发者。
    /// </summary>
    private const string Issuer = "https://issuer.test";

    /// <summary>
    /// 测试用受众。
    /// </summary>
    private const string Audience = "api";

    /// <summary>
    /// 严格选项：校验签发者与受众，时钟偏移为零。
    /// </summary>
    private static readonly JwtOptions _options = new() { Secret = Secret, Issuer = Issuer, Audience = Audience, ClockSkew = TimeSpan.Zero };

    /// <summary>
    /// 签发 HS256 令牌；默认主体 alice、十分钟有效期，可按场景改密钥、签发者、受众、有效期与声明。
    /// </summary>
    private static string CreateToken(string? subject = "alice", string secret = Secret, string issuer = Issuer, string audience = Audience, TimeSpan? lifetime = null, params (string Type, string Value)[] claims)
    {
        var identity = new ClaimsIdentity();
        if (subject is not null)
        {
            identity.AddClaim(new Claim("sub", subject));
        }

        foreach (var (type, value) in claims)
        {
            identity.AddClaim(new Claim(type, value));
        }

        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = identity,
            NotBefore = DateTime.UtcNow.AddMinutes(-5),
            Expires = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(10)),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256),
        });
    }

    /// <summary>
    /// 用给定选项（默认严格选项）认证 Bearer 令牌。
    /// </summary>
    private static async Task<Result<Principal>> AuthenticateAsync(string token, JwtOptions? options = null)
        => await new JwtAuthenticationHandler(options ?? _options).AuthenticateAsync(new AuthenticationCredential("Bearer", token), TestInvocationContext.Create(), CancellationToken.None);

    [Fact]
    public async Task ValidToken_BecomesPrincipalWithRawClaims()
    {
        var principal = (await AuthenticateAsync(CreateToken(claims: [("role", "editor"), ("role", "auditor"), ("name", "Alice"), ("tenant", "acme")]))).ValueOrThrow();

        Assert.Equal("alice", principal.Id);
        Assert.Equal("Bearer", principal.AuthenticationScheme);
        Assert.True(principal.IsAuthenticated);
        Assert.Equal("Alice", principal.DisplayName);
        Assert.Equal(["editor", "auditor"], principal.ClaimValues("role"));
        Assert.Equal(["acme"], principal.ClaimValues("tenant"));
        Assert.Contains(principal.Claims, static c => c.Key == "iss" && c.Value == Issuer);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("wrong-secret")]
    [InlineData("wrong-audience")]
    [InlineData("wrong-issuer")]
    [InlineData("garbage")]
    [InlineData("no-subject")]
    public async Task InvalidTokens_AreDenied(string scenario)
    {
        var token = scenario switch
        {
            "expired" => CreateToken(lifetime: TimeSpan.FromMinutes(-1)),
            "wrong-secret" => CreateToken(secret: "another-secret-key-with-at-least-32-bytes!!"),
            "wrong-audience" => CreateToken(audience: "other"),
            "wrong-issuer" => CreateToken(issuer: "https://evil.test"),
            "no-subject" => CreateToken(subject: null),
            _ => "not.a.token",
        };

        var result = await AuthenticateAsync(token);

        Assert.True(result.IsFailure);
        Assert.Equal(SecurityErrors.InvalidCredential, result.Error?.Code);
    }

    [Fact]
    public async Task IssuerAndAudience_AreOnlyValidatedWhenConfigured()
    {
        var lenient = new JwtOptions { Secret = Secret, ClockSkew = TimeSpan.Zero };

        Assert.True((await AuthenticateAsync(CreateToken(issuer: "anyone", audience: "anything"), lenient)).IsSuccess);
    }

    [Fact]
    public void Options_FromConfiguration_RequireSecret_AndParseRest()
    {
        // 用内存键值对构造配置
        static IConfiguration Build(params (string Key, string Value)[] values)
            => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

        var options = JwtOptions.FromConfiguration(Build(("XiHan:Security:Jwt:Secret", Secret), ("XiHan:Security:Jwt:Issuer", Issuer), ("XiHan:Security:Jwt:ClockSkewSeconds", "5"), ("XiHan:Security:Jwt:NameClaim", "uid")));
        Assert.Equal(Issuer, options.Issuer);
        Assert.Null(options.Audience);
        Assert.Equal(TimeSpan.FromSeconds(5), options.ClockSkew);
        Assert.Equal("uid", options.NameClaim);

        Assert.Throws<ArgumentException>(() => JwtOptions.FromConfiguration(Build()));
        Assert.Throws<ArgumentException>(() => JwtOptions.FromConfiguration(Build(("XiHan:Security:Jwt:Secret", "short"))));
        Assert.Throws<ArgumentException>(() => JwtOptions.FromConfiguration(Build(("XiHan:Security:Jwt:Secret", Secret), ("XiHan:Security:Jwt:ClockSkewSeconds", "-1"))));
    }

    [Fact]
    public async Task Feature_TakesOverAuthentication_WhileInMemoryKeepsAuthorization()
    {
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemorySecurityFeature>().UseFeature<JwtSecurityFeature>().UseFeature(new StubFeature(TestManifests.Create("xihan.app.orders", requires: ["security.authorization@1"]))
            {
                OnConfigure = static ctx => ctx.AddPermission(new PermissionDefinition("orders.read", "读订单")),
            }),
            [
                new("XiHan:Security:Jwt:Secret", Secret),
                new("XiHan:Security:Jwt:Issuer", Issuer),
                new("XiHan:Security:Jwt:Audience", Audience),
                new("XiHan:Security:InMemory:Grants:role:editor", "orders.read"),
            ]);

        Assert.IsType<JwtAuthenticationHandler>(Assert.Single(host.Services.GetServices<IAuthenticationHandler>()));
        Assert.Equal(1, host.App.DescribePipeline().Split(nameof(AuthenticationMiddleware)).Length - 1);
        Assert.Contains(nameof(AuthorizationMiddleware), host.App.DescribePipeline(), StringComparison.Ordinal);

        await using var scope = host.Services.CreateAsyncScope();
        var context = App.CreateContext(scope.ServiceProvider, CancellationToken.None);
        context.SetCredential(new AuthenticationCredential("Bearer", CreateToken(claims: [("role", "editor")])));
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
