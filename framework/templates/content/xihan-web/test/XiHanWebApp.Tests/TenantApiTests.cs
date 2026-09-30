using System.Net;
using XiHanWebApp.MultiTenancy;

namespace XiHanWebApp.Tests;

/// <summary>
/// 租户解析测试
/// </summary>
public class TenantApiTests(WebAppHostFactory factory) : IClassFixture<WebAppHostFactory>
{
    [Fact]
    public async Task GetCurrent_WithoutTenantHeader_IsPlatform()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/Tenant/Current", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.ReadApiResultAsync<CurrentTenantDto>(cancellationToken);
        Assert.Null(result.Data?.Id);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("demo")]
    public async Task GetCurrent_WithRegisteredTenant_ResolvesTenant(string tenantKey)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantKey);

        using var response = await client.GetAsync("/api/Tenant/Current", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.ReadApiResultAsync<CurrentTenantDto>(cancellationToken);
        Assert.Equal(1, result.Data?.Id);
        Assert.Equal("demo", result.Data?.Name);
    }

    [Fact]
    public async Task GetCurrent_WithUnknownTenant_IsRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "999");

        using var response = await client.GetAsync("/api/Tenant/Current", cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
