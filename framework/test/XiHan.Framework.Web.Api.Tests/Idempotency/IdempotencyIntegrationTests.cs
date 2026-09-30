// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Security.Users;
using XiHan.Framework.Uow;
using XiHan.Framework.Uow.Abstracts;
using XiHan.Framework.Uow.Options;
using XiHan.Framework.Web.Api.Extensions.DependencyInjection;
using XiHan.Framework.Web.Api.Filters;
using XiHan.Framework.Web.Api.Idempotency;

namespace XiHan.Framework.Web.Api.Tests.Idempotency;

/// <summary>
/// 幂等保护的 TestServer 集成测试
/// </summary>
public sealed class IdempotencyIntegrationTests : IAsyncLifetime
{
    private const string TestScheme = "TestAuth";
    private const string OrdersPath = "/api/idempotency-sample/orders";

    private readonly ExecutionCounter _counter = new();
    private IHost _host = null!;
    private HttpClient _client = null!;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var configuration = new ConfigurationBuilder().Build();
        _host = await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddLogging();
                    services.AddHttpContextAccessor();
                    services.AddAuthentication(TestScheme).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestScheme, null);
                    services.AddAuthorization();
                    services.AddOptions<XiHanUnitOfWorkDefaultOptions>();
                    services.AddSingleton<IAmbientUnitOfWork, AmbientUnitOfWork>();
                    services.AddSingleton<IUnitOfWorkManager, UnitOfWorkManager>();
                    services.AddSingleton<IUnitOfWorkEventPublisher, NullUnitOfWorkEventPublisher>();
                    services.AddSingleton<IUnitOfWorkTransactionBehaviourProvider, NullUnitOfWorkTransactionBehaviourProvider>();
                    services.AddTransient<IUnitOfWork, UnitOfWork>();
                    services.AddSingleton(_counter);
                    services.AddSingleton<ICurrentUser, TestCurrentUser>();
                    services.AddSingleton<ICurrentTenant, FakeCurrentTenant>();
                    services.AddScoped<XiHanApiResponseResultFilter>();
                    services.AddScoped<XiHanUnitOfWorkFilter>();
                    services.AddXiHanWebApiIdempotency(configuration);
                    services.AddControllers(options =>
                    {
                        options.Filters.AddService<XiHanApiResponseResultFilter>();
                        options.Filters.AddService<XiHanIdempotencyFilter>();
                        options.Filters.AddService<XiHanUnitOfWorkFilter>();
                        options.Filters.AddService<XiHanIdempotencyCompletionFilter>();
                    }).AddApplicationPart(typeof(IdempotencySampleController).Assembly);
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            })
            .StartAsync();
        _client = _host.GetTestClient();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _counter.Open();
        _client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }

    [Fact]
    public async Task ConcurrentRequests_OnlyOneRunsAndLaterOneReplays()
    {
        _counter.Close();
        var first = SendAsync("user-1", "key-1", "SKU-A");
        await WaitForCountAsync(1);

        var conflicts = await Task.WhenAll(Enumerable.Range(0, 19).Select(_ => SendAsync("user-1", "key-1", "SKU-A")));
        Assert.All(conflicts, response => Assert.Equal(HttpStatusCode.Conflict, response.StatusCode));

        _counter.Open();
        var firstResponse = await first;
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstData = (await ReadDataAsync(firstResponse)).GetRawText();

        var replay = await SendAsync("user-1", "key-1", "SKU-A");
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.True(replay.Headers.TryGetValues(XiHanIdempotencyFilter.ReplayedHeaderName, out var values) && values.Contains("true"));
        Assert.Equal(firstData, (await ReadDataAsync(replay)).GetRawText());
        Assert.Equal(1, _counter.Count);
    }

    [Fact]
    public async Task BurstOf20_ActionRunsAtMostOnce()
    {
        using var start = new ManualResetEventSlim(false);
        var tasks = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(async () =>
            {
                start.Wait();
                return await SendAsync("user-1", "burst-key", "SKU-A");
            }))
            .ToArray();
        start.Set();
        var responses = await Task.WhenAll(tasks);

        Assert.Equal(1, _counter.Count);
        Assert.All(responses, response => Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
    }

    [Fact]
    public async Task SameKeyDifferentBody_Returns409()
    {
        Assert.Equal(HttpStatusCode.OK, (await SendAsync("user-1", "key-2", "SKU-A")).StatusCode);

        var response = await SendAsync("user-1", "key-2", "SKU-B");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, _counter.Count);
    }

    [Fact]
    public async Task DifferentUsersSameKey_DoNotShareResponse()
    {
        var first = await SendAsync("user-1", "shared-key", "SKU-A");
        var second = await SendAsync("user-2", "shared-key", "SKU-A");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(2, _counter.Count);
        Assert.NotEqual(
            (await ReadDataAsync(first)).GetProperty("orderNo").GetInt32(),
            (await ReadDataAsync(second)).GetProperty("orderNo").GetInt32());
    }

    [Fact]
    public async Task UnauthenticatedWithExistingKey_Returns401NotReplay()
    {
        var first = await SendAsync("user-1", "key-3", "SKU-A");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var response = await SendAsync(null, "key-3", "SKU-A");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("orderNo", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, _counter.Count);
    }

    [Fact]
    public async Task MissingKey_Returns400()
    {
        var response = await SendAsync("user-1", null, "SKU-A");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, _counter.Count);
    }

    private Task<HttpResponseMessage> SendAsync(string? user, string? key, string sku)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, OrdersPath)
        {
            Content = new StringContent($$"""{"sku":"{{sku}}","quantity":1}""", Encoding.UTF8, "application/json")
        };
        if (user is not null)
        {
            request.Headers.Add("X-Test-User", user);
        }

        if (key is not null)
        {
            request.Headers.Add("Idempotency-Key", key);
        }

        return _client.SendAsync(request);
    }

    private async Task WaitForCountAsync(int expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (_counter.Count < expected && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.Equal(expected, _counter.Count);
    }

    private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-User", out var user) || string.IsNullOrEmpty(user))
            {
                return Task.FromResult(AuthenticateResult.Fail("缺少 X-Test-User"));
            }

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.ToString())], TestScheme);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), TestScheme)));
        }
    }

    private sealed class TestCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
    {
        private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

        public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

        public long? UserId => Principal?.FindFirst(ClaimTypes.NameIdentifier) is { } claim
            ? long.Parse(claim.Value.Replace("user-", string.Empty))
            : null;

        public string? UserName => null;

        public string? Name => null;

        public string? SurName => null;

        public string? PhoneNumber => null;

        public bool PhoneNumberVerified => false;

        public string? Email => null;

        public bool EmailVerified => false;

        public long? TenantId => null;

        public string[] Roles => [];

        public Claim? FindClaim(string claimType)
        {
            return Principal?.FindFirst(claimType);
        }

        public Claim[] FindClaims(string claimType)
        {
            return Principal?.FindAll(claimType).ToArray() ?? [];
        }

        public Claim[] GetAllClaims()
        {
            return Principal?.Claims.ToArray() ?? [];
        }

        public bool IsInRole(string roleName)
        {
            return false;
        }
    }
}
