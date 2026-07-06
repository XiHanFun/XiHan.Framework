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
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using XiHan.Framework.AI.Abstractions.Configuration;
using XiHan.Framework.AI.Abstractions.Guardrails;
using XiHan.Framework.AI.Extensions;

namespace XiHan.Framework.AI.Providers;

/// <summary>
/// OpenAI 兼容 <see cref="IChatClient"/> 工厂
/// </summary>
/// <remarks>
/// 一个适配器打天下:OpenAI/DeepSeek/Azure/vLLM/自训模型皆走 OpenAI 兼容协议——
/// 设 <see cref="AiProviderOptions.BaseUrl"/> 指向对应端点 + Model + ApiKey 即可。
/// 管道由外到内(注册顺序=外→内):护栏 → 遥测 → 缓存 → 工具调用;横切三件由 <c>XiHan:AI:Pipeline</c> 开关,默认关。
/// </remarks>
public sealed class OpenAiCompatibleChatClientFactory
{
    private readonly IOptions<XiHanAiOptions> _aiOptions;
    private readonly IOptions<AiGuardrailOptions> _guardrailOptions;
    private readonly IReadOnlyList<IAiGuardrail> _guardrails;
    private readonly IDistributedCache? _cache;
    private readonly ILoggerFactory? _loggerFactory;

    /// <summary>
    /// 构造函数
    /// </summary>
    public OpenAiCompatibleChatClientFactory(
        IOptions<XiHanAiOptions> aiOptions,
        IOptions<AiGuardrailOptions> guardrailOptions,
        IEnumerable<IAiGuardrail> guardrails,
        IDistributedCache? cache = null,
        ILoggerFactory? loggerFactory = null)
    {
        _aiOptions = aiOptions;
        _guardrailOptions = guardrailOptions;
        _guardrails = guardrails.ToArray();
        _cache = cache;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// 由配置构建 IChatClient（护栏/遥测/缓存按开关 + UseFunctionInvocation 管道）
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

        var pipeline = _aiOptions.Value.Pipeline;
        var builder = openAiChatClient.AsIChatClient().AsBuilder();

        // 注册顺序=外→内(ChatClientBuilder.Build 逆序应用):护栏最外(先见原始输入),缓存置于工具调用之外(命中即跳过工具)
        if (pipeline.EnableGuardrail && _guardrails.Count > 0)
        {
            builder.UseContentGuardrail(_guardrails, _guardrailOptions.Value.RefusalMessage);
        }

        if (pipeline.EnableTelemetry)
        {
            builder.UseOpenTelemetry(sourceName: pipeline.TelemetrySourceName, configure: telemetry => telemetry.EnableSensitiveData = pipeline.EnableSensitiveTelemetry);
        }

        if (pipeline.EnableResponseCache && _cache is not null)
        {
            builder.UseDistributedCache(_cache);
        }

        builder.UseFunctionInvocation(_loggerFactory);

        return builder.Build();
    }
}
