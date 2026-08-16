// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;

namespace XiHan.Framework.Web.Mcp.Tests;

/// <summary>
/// /mcp 端点的密钥守门：认得的两种携带形式都放行，其余一律 401
/// </summary>
/// <remarks>
/// 端点上挂了 <c>AllowAnonymous()</c> 绕开框架的全局鉴权 FallbackPolicy，
/// 于是这道过滤器是这个网络可达端点唯一的门。它一旦恒真放行，端点就是敞开的，
/// 而除了这些测试没有别的东西会因此变红。
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
    /// <remarks>
    /// 断言到 tools/list 而不是停在 401 的反面：过滤器放行之后请求还得真进得了 MCP 处理器，
    /// 只看「不是 401」的话，一个放行到 500 的实现也算过。
    /// </remarks>
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
    /// README 把这一条写成了对外承诺，而它在实现里是一段独立的回退分支：
    /// 只有 HeaderName 指定的头缺失或为空时才去看 Authorization。删掉那段分支，
    /// 上面那条用默认请求头的测试照样全绿，只有这一条会红。
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
    /// 空值走的是与「值不对」不同的分支——先被判空、根本到不了定长比较，
    /// 所以要单独钉一条，否则把判空去掉时不会有测试变红。
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
    /// <c>StringValues.ToString()</c> 会把多个值用逗号拼成一串，于是「a」加「b」读出来是「a,b」，
    /// 与期望密钥不等而被拒。这条钉住的是：不能改成「任意一个值匹配就放行」——
    /// 那样只要在正确密钥旁边多塞一个头就能绕过任何基于整串值的审计与限流。
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
