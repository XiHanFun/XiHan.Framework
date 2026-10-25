// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;
using XiHan.Framework.AI.Abstractions.Providers;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Tests;

/// <summary>
/// <c>Kind = fake</c> 的聊天工厂：记录创建过的客户端。
/// </summary>
internal sealed class FakeChatClientFactory : IChatClientFactory
{
    /// <summary>
    /// 创建过的客户端。
    /// </summary>
    public List<FakeChatClient> Created { get; } = [];

    /// <inheritdoc />
    public string Kind => "fake";

    /// <inheritdoc />
    public Result<IChatClient> Create(AIProviderOptions options)
    {
        if (string.IsNullOrEmpty(options.ChatModel))
        {
            return Error.Validation(AIErrors.RequestInvalid, AIMessages.ProviderInvalid, MessageArguments.Of(("name", options.Name), ("reason", "ChatModel is required")));
        }

        var client = new FakeChatClient();
        Created.Add(client);
        return Result.Success<IChatClient>(client);
    }
}
