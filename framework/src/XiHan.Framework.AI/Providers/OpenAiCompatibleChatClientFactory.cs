#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:OpenAiCompatibleChatClientFactory
// Guid:b2c3d4e5-f6a7-4b01-9d01-1a1b1c1d1e01
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/05 12:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using XiHan.Framework.AI.Abstractions.Configuration;

namespace XiHan.Framework.AI.Providers;

/// <summary>
/// OpenAI 兼容 <see cref="IChatClient"/> 工厂
/// </summary>
/// <remarks>
/// 一个适配器打天下:OpenAI/DeepSeek/Azure/vLLM/自训模型皆走 OpenAI 兼容协议——
/// 设 <see cref="AiProviderOptions.BaseUrl"/> 指向对应端点 + Model + ApiKey 即可。
/// 构建后套 UseFunctionInvocation 中间件,自动执行 <see cref="ChatOptions.Tools"/> 里的工具调用。
/// </remarks>
public sealed class OpenAiCompatibleChatClientFactory
{
    private readonly ILoggerFactory? _loggerFactory;

    /// <summary>
    /// 构造函数
    /// </summary>
    public OpenAiCompatibleChatClientFactory(ILoggerFactory? loggerFactory = null)
    {
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// 由配置构建 IChatClient（含 UseFunctionInvocation 管道）
    /// </summary>
    public IChatClient Create(AiProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            throw new InvalidOperationException($"AI Provider [{options.Provider}] 未配置 Model。");
        }

        // 本地/兼容端点常不校验 key，用占位符避免 ApiKeyCredential 空值报错；云端用真实 key
        var credential = new ApiKeyCredential(string.IsNullOrWhiteSpace(options.ApiKey) ? "no-key" : options.ApiKey);

        var openAiChatClient = string.IsNullOrWhiteSpace(options.BaseUrl)
            ? new ChatClient(options.Model, credential)
            : new ChatClient(options.Model, credential, new OpenAIClientOptions { Endpoint = new Uri(options.BaseUrl) });

        return openAiChatClient
            .AsIChatClient()
            .AsBuilder()
            .UseFunctionInvocation(_loggerFactory)
            .Build();
    }
}
