// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace XiHan.Framework.Scheduling.Kubernetes.Tests;

/// <summary>
/// 内存里的 API Server 桩。
/// </summary>
/// <remarks>
/// 只实现 Lease 的 GET / POST / PUT，带 resourceVersion 乐观并发、令牌校验，可注入一次失败。
/// </remarks>
internal sealed class FakeLeaseApi : HttpMessageHandler
{
    /// <summary>
    /// 期望的 Bearer 令牌。
    /// </summary>
    public const string Token = "test-token";

    /// <summary>
    /// 保护表的锁。
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// 名字 → Lease JSON（含 resourceVersion）。
    /// </summary>
    private readonly Dictionary<string, JsonObject> _leases = new(StringComparer.Ordinal);

    /// <summary>
    /// 递增的资源版本。
    /// </summary>
    private int _version;

    /// <summary>
    /// 为真时下一次请求返回 500。
    /// </summary>
    public bool FailNext { get; set; }

    /// <summary>
    /// 收到的请求数。
    /// </summary>
    public int Requests { get; private set; }

    /// <summary>
    /// 当前 Lease 的 spec（测试断言用）。
    /// </summary>
    public JsonObject? Spec(string name)
    {
        lock (_lock)
        {
            return _leases.TryGetValue(name, out var lease) ? (JsonObject)lease["spec"]!.DeepClone() : null;
        }
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests++;
        if (request.Headers.Authorization is not { Scheme: "Bearer", Parameter: Token })
        {
            return Status(HttpStatusCode.Unauthorized, "Unauthorized", "missing or wrong token");
        }

        if (FailNext)
        {
            FailNext = false;
            return Status(HttpStatusCode.InternalServerError, "InternalError", "injected failure");
        }

        var segments = request.RequestUri!.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        // apis/coordination.k8s.io/v1/namespaces/{ns}/leases[/{name}]
        if (segments.Length < 6 || segments[0] != "apis" || segments[1] != "coordination.k8s.io" || segments[5] != "leases")
        {
            return Status(HttpStatusCode.NotFound, "NotFound", "unknown path");
        }

        var name = segments.Length > 6 ? segments[6] : null;
        var body = request.Content is null ? null : JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellationToken)) as JsonObject;
        lock (_lock)
        {
            if (request.Method == HttpMethod.Get && name is not null)
            {
                return _leases.TryGetValue(name, out var found) ? Json(HttpStatusCode.OK, found) : Status(HttpStatusCode.NotFound, "NotFound", $"leases.coordination.k8s.io \"{name}\" not found");
            }

            if (request.Method == HttpMethod.Post && name is null && body is not null)
            {
                var created = (string)body["metadata"]!["name"]!;
                if (_leases.ContainsKey(created))
                {
                    return Status(HttpStatusCode.Conflict, "AlreadyExists", $"leases.coordination.k8s.io \"{created}\" already exists");
                }

                body["metadata"]!["resourceVersion"] = (++_version).ToString(CultureInfo.InvariantCulture);
                _leases[created] = body;
                return Json(HttpStatusCode.Created, body);
            }

            if (request.Method == HttpMethod.Put && name is not null && body is not null)
            {
                if (!_leases.TryGetValue(name, out var current))
                {
                    return Status(HttpStatusCode.NotFound, "NotFound", $"leases.coordination.k8s.io \"{name}\" not found");
                }

                // 乐观并发：带的版本必须是当前版本
                if ((string?)body["metadata"]?["resourceVersion"] != (string?)current["metadata"]!["resourceVersion"])
                {
                    return Status(HttpStatusCode.Conflict, "Conflict", "the object has been modified; please apply your changes to the latest version and try again");
                }

                body["metadata"]!["resourceVersion"] = (++_version).ToString(CultureInfo.InvariantCulture);
                _leases[name] = body;
                return Json(HttpStatusCode.OK, body);
            }
        }

        return Status(HttpStatusCode.MethodNotAllowed, "MethodNotAllowed", request.Method.Method);
    }

    /// <summary>
    /// JSON 应答。
    /// </summary>
    private static HttpResponseMessage Json(HttpStatusCode status, JsonObject body)
        => new(status) { Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json") };

    /// <summary>
    /// Status 应答。
    /// </summary>
    private static HttpResponseMessage Status(HttpStatusCode status, string reason, string message)
        => new(status) { Content = new StringContent(JsonSerializer.Serialize(new JsonObject { ["kind"] = "Status", ["reason"] = reason, ["message"] = message }), System.Text.Encoding.UTF8, "application/json") };
}
