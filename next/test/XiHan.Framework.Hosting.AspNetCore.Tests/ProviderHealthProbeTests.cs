// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using XiHan.Framework.Caching.InMemory;

namespace XiHan.Framework.Hosting.AspNetCore.Tests;

/// <summary>
/// 探针端点把 Provider 的健康项写进响应。
/// </summary>
/// <remarks>
/// <c>/health/ready</c> 的 <c>contributions</c> 里出现 <c>caching.inmemory.capacity</c>。
/// </remarks>
public sealed class ProviderHealthProbeTests
{
    [Fact]
    public async Task ReadyProbe_ListsProviderHealthItems_InContributions()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.AddXiHan(static xihan => xihan.UseFeature<InMemoryCachingFeature>());
        var app = builder.Build();
        await using var _ = app;
        app.UseXiHan();
        app.MapXiHanHealth();
        await app.StartAsync();

        var response = await app.GetTestClient().GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("pass", body.RootElement.GetProperty("status").GetString());
        var contribution = Assert.Single(body.RootElement.GetProperty("contributions").EnumerateArray(), static c => c.GetProperty("id").GetString() == InMemoryCachingFeature.HealthName);
        Assert.Equal("Ready", contribution.GetProperty("state").GetString());
    }
}
