using System.Net;

namespace XiHanWebApp.Tests;

/// <summary>
/// 健康检查端点测试
/// </summary>
public class HealthCheckTests(WebAppHostFactory factory) : IClassFixture<WebAppHostFactory>
{
    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(WebAppHostModule.HealthCheckPath, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(cancellationToken));
    }
}
