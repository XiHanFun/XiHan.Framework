// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace XiHan.Framework.AI.OpenAI.Tests;

/// <summary>
/// 仿真 OpenAI 兼容端点：<c>/chat/completions</c>（可流式、可先要求调用工具）与 <c>/embeddings</c>；记录全部请求。
/// </summary>
internal sealed class FakeOpenAIHandler : HttpMessageHandler
{
    /// <summary>
    /// 收到的请求：(路径, 正文)。
    /// </summary>
    public List<(string Path, JsonNode Body)> Requests { get; } = [];

    /// <summary>
    /// 最终应答。
    /// </summary>
    public string Reply { get; set; } = "pong";

    /// <summary>
    /// 请求带工具且还没有工具结果时，先要求调用这个工具：(名字, 参数 JSON)。
    /// </summary>
    public (string Name, string Arguments)? ToolToCall { get; set; }

    /// <summary>
    /// 没有指定维度时的嵌入维度。
    /// </summary>
    public int DefaultDimensions { get; set; } = 8;

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!;
        Requests.Add((path, body));
        if (path.EndsWith("/embeddings", StringComparison.Ordinal))
        {
            return Json(Embeddings(body));
        }

        if (!path.EndsWith("/chat/completions", StringComparison.Ordinal))
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var messages = body["messages"]!.AsArray();
        var last = messages[^1]!;
        var lastRole = last["role"]!.GetValue<string>();
        var hasTools = body["tools"] is JsonArray { Count: > 0 };
        var stream = body["stream"]?.GetValue<bool>() == true;
        string? content = null;
        (string Name, string Arguments)? call = null;
        if (lastRole == "tool")
        {
            content = "Tool said: " + (last["content"]?.GetValue<string>() ?? string.Empty);
        }
        else if (hasTools && ToolToCall is { } wanted)
        {
            call = wanted;
        }
        else
        {
            content = Reply;
        }

        return stream ? Sse(content ?? string.Empty) : Json(Completion(content, call));
    }

    /// <summary>
    /// 非流式应答。
    /// </summary>
    private static string Completion(string? content, (string Name, string Arguments)? call)
    {
        var message = new JsonObject { ["role"] = "assistant", ["content"] = content };
        if (call is { } tool)
        {
            message["tool_calls"] = new JsonArray(new JsonObject
            {
                ["id"] = "call_1",
                ["type"] = "function",
                ["function"] = new JsonObject { ["name"] = tool.Name, ["arguments"] = tool.Arguments },
            });
        }

        return new JsonObject
        {
            ["id"] = "chatcmpl-1",
            ["object"] = "chat.completion",
            ["created"] = 0,
            ["model"] = "fake",
            ["choices"] = new JsonArray(new JsonObject { ["index"] = 0, ["message"] = message, ["finish_reason"] = call is null ? "stop" : "tool_calls" }),
            ["usage"] = new JsonObject { ["prompt_tokens"] = 1, ["completion_tokens"] = 1, ["total_tokens"] = 2 },
        }.ToJsonString();
    }

    /// <summary>
    /// 流式应答：两个增量与结束标记。
    /// </summary>
    private static HttpResponseMessage Sse(string content)
    {
        var builder = new StringBuilder();
        foreach (var piece in new[] { content[..(content.Length / 2)], content[(content.Length / 2)..] })
        {
            var chunk = new JsonObject
            {
                ["id"] = "chatcmpl-1",
                ["object"] = "chat.completion.chunk",
                ["created"] = 0,
                ["model"] = "fake",
                ["choices"] = new JsonArray(new JsonObject { ["index"] = 0, ["delta"] = new JsonObject { ["role"] = "assistant", ["content"] = piece }, ["finish_reason"] = null }),
            };
            builder.Append("data: ").Append(chunk.ToJsonString()).Append("\n\n");
        }

        builder.Append("data: [DONE]\n\n");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(builder.ToString(), Encoding.UTF8, "text/event-stream") };
    }

    /// <summary>
    /// 嵌入应答：每个输入一个确定性的向量。
    /// </summary>
    private string Embeddings(JsonNode body)
    {
        var inputs = body["input"] is JsonArray array ? array.Select(static item => item!.GetValue<string>()).ToList() : [body["input"]!.GetValue<string>()];
        var dimensions = body["dimensions"]?.GetValue<int>() ?? DefaultDimensions;
        var data = new JsonArray();
        for (var index = 0; index < inputs.Count; index++)
        {
            var vector = new JsonArray();
            for (var dimension = 0; dimension < dimensions; dimension++)
            {
                vector.Add((JsonNode?)JsonValue.Create(Math.Round((inputs[index].Length + dimension) / 100d, 4)));
            }

            data.Add((JsonNode?)new JsonObject { ["object"] = "embedding", ["index"] = index, ["embedding"] = vector });
        }

        return new JsonObject
        {
            ["object"] = "list",
            ["data"] = data,
            ["model"] = body["model"]?.GetValue<string>() ?? "fake",
            ["usage"] = new JsonObject { ["prompt_tokens"] = inputs.Count, ["total_tokens"] = inputs.Count },
        }.ToJsonString();
    }

    /// <summary>
    /// JSON 应答。
    /// </summary>
    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    /// <summary>
    /// 某条请求里的字符串字段。
    /// </summary>
    public static string? Text(JsonNode body, string property) => body[property]?.GetValue<string>()?.ToString(CultureInfo.InvariantCulture);
}
