// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using XiHan.Framework.AI.Abstractions;
using XiHan.Framework.AI.Abstractions.Providers;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.OpenAI;

/// <summary>
/// <c>Kind = openai</c> 的聊天客户端工厂。
/// </summary>
/// <remarks>
/// <c>OpenAIClient.GetChatClient(ChatModel).AsIChatClient()</c> 外包一层函数调用循环，工具调用自动执行。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class OpenAIChatClientFactory : IChatClientFactory
{
    /// <summary>
    /// 种类。
    /// </summary>
    public const string KindName = "openai";

    /// <summary>
    /// 客户端工厂。
    /// </summary>
    private readonly OpenAIClientFactory _clients;

    /// <summary>
    /// 日志工厂（可无）。
    /// </summary>
    private readonly ILoggerFactory? _loggers;

    /// <summary>
    /// 创建。
    /// </summary>
    public OpenAIChatClientFactory(IHttpClientFactory? httpClients = null, ILoggerFactory? loggers = null)
    {
        _clients = new OpenAIClientFactory(httpClients);
        _loggers = loggers;
    }

    /// <inheritdoc />
    public string Kind => KindName;

    /// <inheritdoc />
    public Result<IChatClient> Create(AIProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ChatModel))
        {
            return Error.Validation(AIErrors.RequestInvalid, AIMessages.ProviderInvalid, MessageArguments.Of(("name", options.Name), ("reason", "ChatModel is required")));
        }

        var client = _clients.Create(options).GetChatClient(options.ChatModel).AsIChatClient();
        return Result.Success(client.AsBuilder().UseFunctionInvocation(_loggers).Build());
    }
}
