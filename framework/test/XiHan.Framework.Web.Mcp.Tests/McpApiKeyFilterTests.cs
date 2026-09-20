// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;

namespace XiHan.Framework.Web.Mcp.Tests;

/// <summary>
/// /mcp 端点的密钥守门：认得的两种携带形式都放行，其余一律 401
/// </summary>
/// <remarks>
/// 端点使用 <c>AllowAnonymous()</c> 绕开框架的全局鉴权 FallbackPolicy，鉴权由该过滤器承担。
/// </remarks>
public class McpApiKeyFilterTests
{
    /// <summary>
    /// 宿主配置的正确密钥
    /// </summary>
    private const string ApiKey = "correct-horse-battery-staple";

    /// <summary>
    /// 默认请求头带对密钥，握手与工具列举都要走得通
    /// </summary>
    [Fact]
    public async Task 默认请求头带对密钥可完成握手并列举工具()
    {
        await using var host = await McpTestHost.StartAsync(enabled: true, ApiKey, new EchoAiSkill());

        await using var session = await host.ConnectAsync("X-Api-Key", ApiKey);

        Assert.NotNull(session.Client.ServerInfo);

        var tools = await session.Client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotEmpty(tools);
    }

    /// <summary>
    /// Authorization: Bearer 带对密钥，同样走得通
    /// </summary>
    /// <remarks>
    /// 覆盖的回退分支：仅当 HeaderName 指定的头缺失或为空时，才改读 Authorization 头。
    /// </remarks>
    [Fact]
    public async Task Authorization用Bearer带对密钥可完成握手并列举工具()
    {
        await using var host = await McpTestHost.StartAsync(enabled: true, ApiKey, new EchoAiSkill());

        await using var session = await host.ConnectAsync("Authorization", "Bearer " + ApiKey);

        Assert.NotNull(session.Client.ServerInfo);

        var tools = await session.Client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotEmpty(tools);
    }

    /// <summary>
    /// Authorization: Bearer 带错误密钥时返回 401
    /// </summary>
    [Fact]
    public async Task Authorization用Bearer带错误密钥时返回401()
    {
        await using var host = await McpTestHost.StartAsync(enabled: true, ApiKey);

        var response = await host.PostInitializeAsync(
            request => request.Headers.TryAddWithoutValidation("Authorization", "Bearer wrong-key"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Authorization 头缺少 Bearer 前缀时返回 401，即便密钥值本身正确
    /// </summary>
    [Fact]
    public async Task Authorization头缺少Bearer前缀时返回401()
    {
        await using var host = await McpTestHost.StartAsync(enabled: true, ApiKey);

        var response = await host.PostInitializeAsync(
            request => request.Headers.TryAddWithoutValidation("Authorization", ApiKey));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// 密钥值不对时返回 401
    /// </summary>
    [Fact]
    public async Task 密钥不对时返回401()
    {
        await using var host = await McpTestHost.StartAsync(enabled: true, ApiKey);

        var response = await host.PostInitializeAsync(
            request => request.Headers.TryAddWithoutValidation("X-Api-Key", "wrong-key"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// 密钥值为空时返回 401
    /// </summary>
    /// <remarks>
    /// 空值在到达定长比较之前先被判空拒绝，走的是与「值不对」不同的分支。
    /// </remarks>
    [Fact]
    public async Task 密钥为空值时返回401()
    {
        await using var host = await McpTestHost.StartAsync(enabled: true, ApiKey);

        var response = await host.PostInitializeAsync(
            request => request.Headers.TryAddWithoutValidation("X-Api-Key", string.Empty));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// 完全不带密钥请求头时返回 401
    /// </summary>
    [Fact]
    public async Task 完全不带密钥时返回401()
    {
        await using var host = await McpTestHost.StartAsync(enabled: true, ApiKey);

        var response = await host.PostInitializeAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// 同名请求头出现多次时返回 401，哪怕其中一个值是对的
    /// </summary>
    /// <remarks>
    /// <c>StringValues.ToString()</c> 把多个值拼成逗号分隔的字符串后再与预期密钥整体比较，
    /// 而非逐个匹配。
    /// </remarks>
    [Fact]
    public async Task 同名密钥请求头出现多次时返回401()
    {
        await using var host = await McpTestHost.StartAsync(enabled: true, ApiKey);

        var response = await host.PostInitializeAsync(request =>
        {
            _ = request.Headers.TryAddWithoutValidation("X-Api-Key", ApiKey);
            _ = request.Headers.TryAddWithoutValidation("X-Api-Key", "second-value");
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
