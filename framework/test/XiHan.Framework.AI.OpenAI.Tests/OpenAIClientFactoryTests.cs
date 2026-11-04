// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;
using XiHan.Framework.AI.Abstractions.Providers;
using XiHan.Framework.AI.OpenAI.Tests.Fixtures;

namespace XiHan.Framework.AI.OpenAI.Tests;

/// <summary>
/// OpenAI 客户端的传输与超时
/// </summary>
/// <remarks>
/// 容器里有 <see cref="IHttpClientFactory"/> 时经它的命名客户端发请求；超时只以 Provider 的 <c>TimeoutSeconds</c> 为准，命名客户端缺省 100 秒的 <see cref="HttpClient.Timeout"/> 不再截断更长的超时
/// </remarks>
public sealed class OpenAIClientFactoryTests
{
    [Fact]
    public async Task NamedHttpClient_CarriesTheRequests_WithoutItsOwnTimeout()
    {
        var handler = new FakeOpenAIHandler { Reply = "pong" };
        var httpClients = new RecordingHttpClientFactory(handler);
        var options = new AIProviderOptions("local", "openai")
        {
            Endpoint = new Uri("http://localhost/v1"),
            ChatModel = "chat-model",
            Timeout = TimeSpan.FromSeconds(300),
        };

        var client = new OpenAIClientFactory(httpClients).Create(options).GetChatClient(options.ChatModel).AsIChatClient();
        var (name, http) = Assert.Single(httpClients.Created);
        Assert.Equal(OpenAIClientFactory.HttpClientName, name);
        // 命名客户端缺省的 100 秒超时会截断 300 秒的 TimeoutSeconds：超时交给 SDK 的 NetworkTimeout，客户端自己不限时
        Assert.Equal(Timeout.InfiniteTimeSpan, http.Timeout);

        var reply = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "ping")]);
        Assert.Equal("pong", reply.Text);
        Assert.EndsWith("/v1/chat/completions", Assert.Single(handler.Requests).Path, StringComparison.Ordinal);
    }
}
