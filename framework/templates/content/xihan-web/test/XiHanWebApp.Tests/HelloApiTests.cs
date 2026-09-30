using System.Net;

namespace XiHanWebApp.Tests;

/// <summary>
/// 问候接口测试
/// </summary>
public class HelloApiTests(WebAppHostFactory factory) : IClassFixture<WebAppHostFactory>
{
    [Fact]
    public async Task GetGreeting_ReturnsGreetingInApiResult()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/Hello/Greeting?name=XiHan", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.ReadApiResultAsync<string>(cancellationToken);
        Assert.True(result.IsSuccess);
        Assert.Equal("你好，XiHan！", result.Data);
    }
}
