// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;

namespace XiHan.Framework.Web.Mcp.Tests.Filters;

/// <summary>
/// /mcp 端点上密钥守门的端到端覆盖
/// </summary>
/// <remarks>
/// 鉴权矩阵（自定义头、Bearer 回落、大小写、长度、重复头值、空密钥配置）已由
/// <see cref="McpApiKeyEndpointFilterTests"/> 按单元粒度穷举。这里只覆盖单元版覆盖不到的那件事：
/// 该过滤器确实被挂到了真实 /mcp 端点上，故只留放行与拒绝各一条。
/// </remarks>
public class McpApiKeyEndToEndTests
{
    /// <summary>
    /// 宿主配置的正确密钥
    /// </summary>
    private const string ApiKey = "correct-horse-battery-staple";

    /// <summary>
    /// 默认请求头带对密钥，握手与工具列举都要走得通
    /// </summary>
    [Fact]
    public async Task Endpoint_WithMatchingHeaderKey_CompletesHandshakeAndListsTools()
    {
        await using var host = await McpTestHost.StartAsync(enabled: true, ApiKey, new EchoAiSkill());

        await using var session = await host.ConnectAsync("X-Api-Key", ApiKey);

        Assert.NotNull(session.Client.ServerInfo);

        var tools = await session.Client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotEmpty(tools);
    }

    /// <summary>
    /// 密钥值不对时端点返回 401
    /// </summary>
    [Fact]
    public async Task Endpoint_WithWrongHeaderKey_ReturnsUnauthorized()
    {
        await using var host = await McpTestHost.StartAsync(enabled: true, ApiKey);

        var response = await host.PostInitializeAsync(
            request => request.Headers.TryAddWithoutValidation("X-Api-Key", "wrong-key"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
