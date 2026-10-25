// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;

namespace XiHan.Framework.Configuration.Nacos.Tests;

/// <summary>
/// 真实 Nacos 来自环境变量 <c>XIHAN_TEST_NACOS</c>（如 <c>http://localhost:8848</c>），鉴权用 <c>XIHAN_TEST_NACOS_USERNAME</c> / <c>XIHAN_TEST_NACOS_PASSWORD</c>；未设置地址时跳过需要它的用例。
/// </summary>
internal static class NacosTestServer
{
    /// <summary>
    /// 指向测试 Nacos 的环境变量名。
    /// </summary>
    public const string EnvironmentVariable = "XIHAN_TEST_NACOS";

    /// <summary>
    /// 发布配置用的共享 HTTP 客户端。
    /// </summary>
    private static readonly HttpClient _http = new();

    /// <summary>
    /// 环境变量里的服务地址；未设置为 <see langword="null"/>。
    /// </summary>
    public static string? ServiceUrl => Environment.GetEnvironmentVariable(EnvironmentVariable);

    /// <summary>
    /// 返回服务地址；未设置时跳过当前用例。
    /// </summary>
    public static string RequireServiceUrl()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ServiceUrl), $"Set the environment variable '{EnvironmentVariable}' to a Nacos server address to run this test.");
        return ServiceUrl!;
    }

    /// <summary>
    /// 指向测试服务的选项。
    /// </summary>
    public static NacosOptions Options(NacosConfigFormat format = NacosConfigFormat.Json) => new()
    {
        ServerAddresses = [RequireServiceUrl()],
        Username = Environment.GetEnvironmentVariable("XIHAN_TEST_NACOS_USERNAME"),
        Password = Environment.GetEnvironmentVariable("XIHAN_TEST_NACOS_PASSWORD"),
        Format = format,
        LongPollTimeout = TimeSpan.FromSeconds(10),
    };

    /// <summary>
    /// 把键值以 JSON 对象发布为 <paramref name="dataId"/> 的内容（<c>null</c> 值保留为 JSON <c>null</c>）。
    /// </summary>
    public static async Task PublishAsync(string dataId, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken)
    {
        var content = JsonSerializer.Serialize(values);
        await PublishRawAsync(dataId, content, "json", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 发布原始内容。
    /// </summary>
    public static async Task PublishRawAsync(string dataId, string content, string type, CancellationToken cancellationToken)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            new("dataId", dataId),
            new("group", "DEFAULT_GROUP"),
            new("content", content),
            new("type", type),
        };
        var url = RequireServiceUrl().TrimEnd('/') + "/nacos/v1/cs/configs" + await TokenQueryAsync(cancellationToken).ConfigureAwait(false);
        using var response = await _http.PostAsync(url, new FormUrlEncodedContent(form), cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        Assert.True(response.IsSuccessStatusCode && body.Trim() == "true", $"Publishing '{dataId}' failed: {(int)response.StatusCode} {body}");
        await WaitUntilVisibleAsync(dataId, content, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 发布接口返回后内容要经通知落到服务端的本地缓存才可读：轮询到取回的内容与发布的一致为止（最多 10 秒），免得用例读到上一版。
    /// </summary>
    private static async Task WaitUntilVisibleAsync(string dataId, string content, CancellationToken cancellationToken)
    {
        var url = RequireServiceUrl().TrimEnd('/') + "/nacos/v1/cs/configs?dataId=" + Uri.EscapeDataString(dataId) + "&group=DEFAULT_GROUP" + (await TokenQueryAsync(cancellationToken).ConfigureAwait(false)).Replace('?', '&');
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode && await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false) == content)
            {
                return;
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        Assert.Fail($"The published configuration '{dataId}' did not become visible within 10 seconds.");
    }

    /// <summary>
    /// 开启了鉴权时登录并返回 <c>?accessToken=…</c>，否则为空串。
    /// </summary>
    private static async Task<string> TokenQueryAsync(CancellationToken cancellationToken)
    {
        var username = Environment.GetEnvironmentVariable("XIHAN_TEST_NACOS_USERNAME");
        var password = Environment.GetEnvironmentVariable("XIHAN_TEST_NACOS_PASSWORD");
        if (string.IsNullOrWhiteSpace(username) || password is null)
        {
            return string.Empty;
        }

        using var response = await _http.PostAsync(RequireServiceUrl().TrimEnd('/') + "/nacos/v1/auth/login", new FormUrlEncodedContent([new("username", username), new("password", password)]), cancellationToken).ConfigureAwait(false);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        return "?accessToken=" + Uri.EscapeDataString(json.RootElement.GetProperty("accessToken").GetString()!);
    }
}
