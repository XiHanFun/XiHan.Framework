// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;

namespace XiHan.Framework.Docs.Mcp.Web.Tests;

/// <summary>
/// 端点暴露与否的 fail-closed 断言
/// </summary>
/// <remarks>
/// 404 与 401 的区别在这里是有意义的：未就绪的部署应当「根本没有这个端点」，
/// 而不是「有端点但拒绝你」——后者会把这台机器上跑着一个 MCP 服务这件事泄露出去。
/// </remarks>
public class EndpointExposureTests
{
    /// <summary>
    /// 三种「只配了一半」的组合，逐条都必须不暴露端点
    /// </summary>
    public static TheoryData<string, string, string?> 未就绪的三种配法 => new()
    {
        { "开关关闭但配了密钥", "false", "secret-key" },
        { "开关打开但没有密钥", "true", null },
        { "开关打开但密钥全是空白", "true", "   " }
    };

    [Theory]
    [MemberData(nameof(未就绪的三种配法))]
    public async Task 未就绪暴露时端点不存在(string 场景, string enabled, string? apiKey)
    {
        var settings = new List<KeyValuePair<string, string?>>
        {
            new("XiHan:Docs:Mcp:Enabled", enabled)
        };

        if (apiKey is not null)
        {
            settings.Add(new KeyValuePair<string, string?>("XiHan:Docs:Mcp:ApiKey", apiKey));
        }

        await using var host = await DocsMcpWebTestHost.StartAsync([.. settings]);

        // 连密钥都带上：即便请求本身无可挑剔，端点也不该存在
        using var response = await host.SendInitializeAsync(request =>
        {
            if (!string.IsNullOrEmpty(apiKey))
            {
                request.Headers.TryAddWithoutValidation("X-Api-Key", apiKey);
            }
        });

        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound,
            $"{场景}：/mcp 本应不存在，实际返回 {(int)response.StatusCode} {response.StatusCode}。");
    }

    [Fact]
    public async Task 就绪暴露时端点存在()
    {
        await using var host = await DocsMcpWebTestHost.StartAsync(
            new KeyValuePair<string, string?>("XiHan:Docs:Mcp:Enabled", "true"),
            new KeyValuePair<string, string?>("XiHan:Docs:Mcp:ApiKey", "secret-key"));

        using var response = await host.SendInitializeAsync(
            request => request.Headers.TryAddWithoutValidation("X-Api-Key", "secret-key"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task 端点路径可配置()
    {
        await using var host = await DocsMcpWebTestHost.StartAsync(
            new KeyValuePair<string, string?>("XiHan:Docs:Mcp:Enabled", "true"),
            new KeyValuePair<string, string?>("XiHan:Docs:Mcp:ApiKey", "secret-key"),
            new KeyValuePair<string, string?>("XiHan:Docs:Mcp:Path", "/docs-mcp"));

        using var 默认路径 = await host.Client.SendAsync(
            DocsMcpWebTestHost.CreateInitializeRequest(),
            HttpCompletionOption.ResponseHeadersRead,
            TestContext.Current.CancellationToken);

        var 自定路径请求 = DocsMcpWebTestHost.CreateInitializeRequest("/docs-mcp");
        自定路径请求.Headers.TryAddWithoutValidation("X-Api-Key", "secret-key");
        using var 自定路径 = await host.Client.SendAsync(
            自定路径请求,
            HttpCompletionOption.ResponseHeadersRead,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, 默认路径.StatusCode);
        Assert.Equal(HttpStatusCode.OK, 自定路径.StatusCode);
    }
}
