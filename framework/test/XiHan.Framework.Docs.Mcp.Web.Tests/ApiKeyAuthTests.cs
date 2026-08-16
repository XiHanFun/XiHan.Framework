// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;

namespace XiHan.Framework.Docs.Mcp.Web.Tests;

/// <summary>
/// 端点鉴权：两种放行写法与四种拒绝情形
/// </summary>
/// <remarks>
/// 被测的 <c>McpApiKeyEndpointFilter</c> 不是本项目的文件，而是
/// <c>framework/src/XiHan.Framework.Web.Mcp/Filters/McpApiKeyEndpointFilter.cs</c> 经 csproj 的
/// <c>&lt;Compile Link&gt;</c> 编进来的同一份源码。所以下面这几条——请求头写法、Bearer 回退、多值请求头——
/// 按住的是两个程序集共用的那一份实现，而不是一份副本。
/// </remarks>
public class ApiKeyAuthTests : IAsyncLifetime
{
    private const string ApiKey = "correct-horse-battery-staple";

    private DocsMcpWebTestHost _host = null!;

    /// <summary>
    /// 起一个已就绪暴露的宿主，全组用例共用
    /// </summary>
    /// <returns>异步任务</returns>
    public async ValueTask InitializeAsync()
    {
        _host = await DocsMcpWebTestHost.StartAsync(
            new KeyValuePair<string, string?>("XiHan:Docs:Mcp:Enabled", "true"),
            new KeyValuePair<string, string?>("XiHan:Docs:Mcp:ApiKey", ApiKey));
    }

    /// <summary>
    /// 停掉宿主
    /// </summary>
    /// <returns>异步任务</returns>
    public async ValueTask DisposeAsync()
    {
        await _host.DisposeAsync();
    }

    [Fact]
    public async Task 请求头携带正确密钥可放行()
    {
        using var response = await _host.SendInitializeAsync(
            request => request.Headers.TryAddWithoutValidation("X-Api-Key", ApiKey));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Bearer携带正确密钥可放行()
    {
        using var response = await _host.SendInitializeAsync(
            request => request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {ApiKey}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task 密钥不对返回401()
    {
        using var response = await _host.SendInitializeAsync(
            request => request.Headers.TryAddWithoutValidation("X-Api-Key", "wrong-key"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task 密钥为空串返回401()
    {
        using var response = await _host.SendInitializeAsync(
            request => request.Headers.TryAddWithoutValidation("X-Api-Key", string.Empty));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task 完全不带鉴权头返回401()
    {
        using var response = await _host.SendInitializeAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Bearer之外的授权方案返回401()
    {
        using var response = await _host.SendInitializeAsync(
            request => request.Headers.TryAddWithoutValidation("Authorization", $"Basic {ApiKey}"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// 手写报文的对照组：单个正确密钥必须放行
    /// </summary>
    /// <remarks>
    /// 没有这一条的话，下面那条多值用例拿到 401 也说明不了问题——可能只是手写的报文本身不合法。
    /// </remarks>
    /// <returns>异步任务</returns>
    [Fact]
    public async Task 手写报文单个正确密钥可放行()
    {
        var status = await _host.SendRawInitializeAsync($"X-Api-Key: {ApiKey}");

        Assert.Equal(200, status);
    }

    /// <summary>
    /// 同一请求上真的送两行 X-Api-Key，其中一行是对的
    /// </summary>
    /// <remarks>
    /// 服务端拿到的 <c>StringValues</c> 有两个元素，<c>ToString()</c> 会用逗号拼成一个串，
    /// 与真密钥不等，因此必须 401。这条挡的是「多塞一个正确值就能混过去」这类绕过尝试——
    /// 若把读取改成 <c>FirstOrDefault()</c>，第一行的正确值就会被单独取出来放行，本条即变红。
    /// <para>
    /// 必须手写报文：<see cref="HttpClient"/> 会在发出前把同名头折成一行，两行头根本上不了线。
    /// </para>
    /// </remarks>
    /// <returns>异步任务</returns>
    [Fact]
    public async Task 多值请求头返回401()
    {
        var status = await _host.SendRawInitializeAsync(
            $"X-Api-Key: {ApiKey}",
            "X-Api-Key: second-value");

        Assert.Equal(401, status);
    }

    /// <summary>
    /// 正确值排在第二行时同样必须 401
    /// </summary>
    /// <returns>异步任务</returns>
    [Fact]
    public async Task 多值请求头顺序颠倒同样返回401()
    {
        var status = await _host.SendRawInitializeAsync(
            "X-Api-Key: first-value",
            $"X-Api-Key: {ApiKey}");

        Assert.Equal(401, status);
    }

    [Fact]
    public async Task 请求头名可配置()
    {
        await using var host = await DocsMcpWebTestHost.StartAsync(
            new KeyValuePair<string, string?>("XiHan:Docs:Mcp:Enabled", "true"),
            new KeyValuePair<string, string?>("XiHan:Docs:Mcp:ApiKey", ApiKey),
            new KeyValuePair<string, string?>("XiHan:Docs:Mcp:HeaderName", "X-Docs-Key"));

        using var 旧头 = await host.SendInitializeAsync(
            request => request.Headers.TryAddWithoutValidation("X-Api-Key", ApiKey));
        using var 新头 = await host.SendInitializeAsync(
            request => request.Headers.TryAddWithoutValidation("X-Docs-Key", ApiKey));

        Assert.Equal(HttpStatusCode.Unauthorized, 旧头.StatusCode);
        Assert.Equal(HttpStatusCode.OK, 新头.StatusCode);
    }
}
